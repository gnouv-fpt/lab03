(function () {
    const shell = document.querySelector(".app-shell");
    if (!shell) {
        return;
    }

    const messagesEl = document.getElementById("messages");
    const messageTemplate = document.getElementById("messageTemplate");
    const messageInput = document.getElementById("messageInput");
    const connectButton = document.getElementById("connectButton");
    const sendButton = document.getElementById("sendButton");
    const fileInput = document.getElementById("fileInput");
    const emojiToggle = document.getElementById("emojiToggle");
    const emojiPanel = document.getElementById("emojiPanel");
    const uploadQueue = document.getElementById("uploadQueue");
    const imageDialog = document.getElementById("imageDialog");
    const dialogImage = document.getElementById("dialogImage");
    const closeImageDialog = document.getElementById("closeImageDialog");
    const serverUrlInput = document.getElementById("serverUrl");
    const displayNameInput = document.getElementById("displayName");
    const statusPrimary = document.getElementById("statusPrimary");
    const statusSecondary = document.getElementById("statusSecondary");
    const chatHeaderStatus = document.getElementById("chatHeaderStatus");
    const messageCount = document.getElementById("messageCount");
    const uploadCount = document.getElementById("uploadCount");

    const storageKeys = {
        serverUrl: "chatclient.serverUrl",
        displayName: "chatclient.displayName"
    };

    let connection = null;
    let currentUser = "";
    let serverBaseUrl = "";
    let pendingUploads = 0;

    hydrateDefaults();
    wireEvents();

    function hydrateDefaults() {
        serverUrlInput.value = localStorage.getItem(storageKeys.serverUrl) || shell.dataset.defaultServer || window.chatClientConfig.defaultServerUrl;
        displayNameInput.value = localStorage.getItem(storageKeys.displayName) || shell.dataset.defaultName || window.chatClientConfig.defaultDisplayName;
    }

    function wireEvents() {
        connectButton.addEventListener("click", connectAsync);
        sendButton.addEventListener("click", sendTextAsync);
        fileInput.addEventListener("change", handleFileSelection);
        emojiToggle.addEventListener("click", () => {
            emojiPanel.hidden = !emojiPanel.hidden;
        });

        document.addEventListener("click", event => {
            if (!emojiPanel.hidden && !emojiPanel.contains(event.target) && event.target !== emojiToggle) {
                emojiPanel.hidden = true;
            }
        });

        messageInput.addEventListener("keydown", async event => {
            if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                await sendTextAsync();
            }
        });

        const picker = emojiPanel.querySelector("emoji-picker");
        picker.addEventListener("emoji-click", event => {
            const emoji = event.detail?.unicode || "";
            insertAtCursor(messageInput, emoji);
            emojiPanel.hidden = true;
            messageInput.focus();
        });

        closeImageDialog.addEventListener("click", () => imageDialog.close());
    }

    async function connectAsync() {
        const normalizedUrl = normalizeServerBaseUrl(serverUrlInput.value);
        if (!normalizedUrl) {
            setStatus("Dia chi server khong hop le", "Nhap dang http://ip:port");
            serverUrlInput.focus();
            return;
        }

        if (!window.signalR) {
            setStatus("Thieu thu vien SignalR tren browser", "Khong tao duoc ket noi realtime");
            return;
        }

        currentUser = (displayNameInput.value || "Ban").trim() || "Ban";
        serverBaseUrl = normalizedUrl;
        localStorage.setItem(storageKeys.serverUrl, serverBaseUrl);
        localStorage.setItem(storageKeys.displayName, currentUser);

        connectButton.disabled = true;
        setInputsLocked(true);
        setStatus("Dang ket noi...", `Dang vao ${serverBaseUrl} voi ten ${currentUser}`);

        try {
            if (connection) {
                await connection.stop();
            }

            messagesEl.innerHTML = "";
            connection = new signalR.HubConnectionBuilder()
                .withUrl(`${serverBaseUrl}/chatHub`, {
                    withCredentials: false
                })
                .withAutomaticReconnect()
                .build();

            connection.on("HistoryLoaded", history => {
                messagesEl.innerHTML = "";
                history.forEach(message => appendMessage(message));
                syncMessageCount();
                scrollMessagesToBottom();
            });

            connection.on("ReceiveMessage", message => {
                appendMessage(message);
                syncMessageCount();
            });

            connection.onreconnecting(error => {
                setStatus("Mat ket noi, dang thu lai...", error?.message || "Dang co gang ket noi lai");
            });

            connection.onreconnected(() => {
                setStatus(`Da ket noi voi ten ${currentUser}`, "San sang gui tin nhan");
            });

            connection.onclose(error => {
                setInputsLocked(false);
                connectButton.disabled = false;
                setStatus("Da ngat ket noi", error?.message || "Bam Ket noi de vao lai");
            });

            await connection.start();
            await connection.invoke("Join", currentUser);
            setStatus(`Da ket noi voi ten ${currentUser}`, "San sang gui tin nhan");
        } catch (error) {
            setInputsLocked(false);
            connectButton.disabled = false;
            setStatus("Khong ket noi duoc server", getErrorMessage(error));
        }
    }

    async function sendTextAsync() {
        const text = messageInput.value.trim();
        if (!text) {
            return;
        }

        if (!isConnected()) {
            setStatus("Chua ket noi server", "Nhap IP may host server roi bam Ket noi");
            return;
        }

        const payload = text;
        messageInput.value = "";
        await connection.invoke("SendText", crypto.randomUUID().replaceAll("-", ""), payload);
    }

    async function handleFileSelection(event) {
        const files = Array.from(event.target.files || []);
        event.target.value = "";

        for (const file of files) {
            void uploadAndSendAttachmentAsync(file);
        }
    }

    async function uploadAndSendAttachmentAsync(file) {
        if (!isConnected()) {
            setStatus("Chua ket noi server", "Ket noi server truoc khi gui tep");
            return;
        }

        pendingUploads += 1;
        syncUploadCount();
        uploadQueue.hidden = false;

        const item = document.createElement("div");
        item.className = "upload-item";
        item.innerHTML = `
            <div class="upload-header">
                <strong>${escapeHtml(file.name)}</strong>
                <span>Dang gui</span>
            </div>
            <progress class="upload-progress" max="100" value="0"></progress>
        `;
        uploadQueue.appendChild(item);

        try {
            const attachment = await uploadFileAsync(file, progress => {
                item.querySelector(".upload-progress").value = progress;
            });

            const caption = messageInput.value.trim();
            messageInput.value = "";
            await connection.invoke("SendAttachment", crypto.randomUUID().replaceAll("-", ""), caption, attachment);
            item.remove();
        } catch (error) {
            item.querySelector(".upload-header span").textContent = "That bai";
            item.querySelector(".upload-header span").classList.add("error-text");
            setStatus("Gui tep that bai", getErrorMessage(error));
        } finally {
            pendingUploads -= 1;
            syncUploadCount();
            if (!uploadQueue.children.length) {
                uploadQueue.hidden = true;
            }
        }
    }

    function uploadFileAsync(file, onProgress) {
        return new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest();
            xhr.open("POST", `${serverBaseUrl}/api/uploads`);
            xhr.responseType = "json";

            xhr.upload.addEventListener("progress", event => {
                if (!event.lengthComputable) {
                    return;
                }

                const percent = Math.round((event.loaded / event.total) * 100);
                onProgress(percent);
            });

            xhr.addEventListener("load", () => {
                if (xhr.status >= 200 && xhr.status < 300 && xhr.response?.attachment) {
                    onProgress(100);
                    resolve(xhr.response.attachment);
                    return;
                }

                reject(new Error(xhr.responseText || `Upload loi ${xhr.status}`));
            });

            xhr.addEventListener("error", () => reject(new Error("Khong tai len duoc tep")));

            const formData = new FormData();
            formData.append("file", file);
            xhr.send(formData);
        });
    }

    function appendMessage(message) {
        const node = messageTemplate.content.firstElementChild.cloneNode(true);
        const isOwn = normalizeName(message.sender) === normalizeName(currentUser);
        const isSystem = normalizeName(message.sender) === "system";

        node.classList.add(isSystem ? "system" : isOwn ? "self" : "other");
        node.querySelector(".sender").textContent = isSystem ? "System" : message.sender;
        node.querySelector(".time").textContent = formatTime(message.sentAt);

        const textEl = node.querySelector(".text");
        if (message.text) {
            textEl.textContent = message.text;
        } else {
            textEl.hidden = true;
        }

        const attachment = message.attachment;
        if (attachment?.url) {
            const fullUrl = toAbsoluteUrl(serverBaseUrl, attachment.url);
            if (attachment.kind === 1 || attachment.kind === 3 || isImageContent(attachment.contentType)) {
                renderImageAttachment(node, fullUrl, attachment.fileName);
            } else if (attachment.kind === 2 || isVideoContent(attachment.contentType)) {
                renderVideoAttachment(node, fullUrl, attachment.fileName);
            } else {
                renderFileAttachment(node, fullUrl, attachment.fileName, attachment.size);
            }
        }

        messagesEl.appendChild(node);
        scrollMessagesToBottom();
    }

    function renderImageAttachment(node, url, fileName) {
        const box = node.querySelector(".attachment-image");
        const image = box.querySelector(".attachment-image-preview");
        const download = box.querySelector(".attachment-download");

        image.src = url;
        image.alt = fileName || "Anh";
        image.addEventListener("click", () => {
            dialogImage.src = url;
            imageDialog.showModal();
        });

        download.href = url;
        download.download = fileName || "";
        box.hidden = false;
    }

    function renderVideoAttachment(node, url, fileName) {
        const box = node.querySelector(".attachment-video");
        const video = box.querySelector(".attachment-video-preview");
        const open = box.querySelector(".attachment-open");
        const download = box.querySelector(".attachment-download");

        video.src = url;
        open.href = url;
        download.href = url;
        download.download = fileName || "";
        box.hidden = false;
    }

    function renderFileAttachment(node, url, fileName, size) {
        const box = node.querySelector(".attachment-file");
        box.querySelector(".attachment-name").textContent = fileName || "Tep dinh kem";
        box.querySelector(".attachment-size").textContent = formatFileSize(size);
        const download = box.querySelector(".attachment-download");
        download.href = url;
        download.download = fileName || "";
        box.hidden = false;
    }

    function setStatus(primary, secondary) {
        statusPrimary.textContent = primary;
        statusSecondary.textContent = secondary || primary;
        chatHeaderStatus.textContent = secondary || primary;
    }

    function setInputsLocked(isLocked) {
        serverUrlInput.disabled = isLocked;
        displayNameInput.disabled = isLocked;
    }

    function isConnected() {
        return connection && connection.state === "Connected";
    }

    function normalizeServerBaseUrl(value) {
        const trimmed = (value || "").trim();
        if (!trimmed) {
            return "";
        }

        const withScheme = trimmed.includes("://") ? trimmed : `http://${trimmed}`;

        try {
            const url = new URL(withScheme);
            if (url.protocol !== "http:" && url.protocol !== "https:") {
                return "";
            }

            return `${url.protocol}//${url.host}`;
        } catch {
            return "";
        }
    }

    function normalizeName(value) {
        return (value || "").trim().toLowerCase();
    }

    function toAbsoluteUrl(baseUrl, relativeOrAbsolute) {
        return new URL(relativeOrAbsolute, baseUrl).toString();
    }

    function syncMessageCount() {
        messageCount.textContent = String(messagesEl.children.length);
    }

    function syncUploadCount() {
        uploadCount.textContent = String(Math.max(0, pendingUploads));
    }

    function scrollMessagesToBottom() {
        requestAnimationFrame(() => {
            messagesEl.scrollTop = messagesEl.scrollHeight;
        });
    }

    function formatTime(value) {
        const date = new Date(value);
        return Number.isNaN(date.valueOf())
            ? ""
            : date.toLocaleString("vi-VN", {
                hour: "2-digit",
                minute: "2-digit",
                day: "2-digit",
                month: "2-digit"
            });
    }

    function formatFileSize(bytes) {
        if (!bytes) {
            return "0 B";
        }

        const units = ["B", "KB", "MB", "GB"];
        let size = bytes;
        let index = 0;

        while (size >= 1024 && index < units.length - 1) {
            size /= 1024;
            index += 1;
        }

        return `${size.toFixed(size >= 10 || index === 0 ? 0 : 1)} ${units[index]}`;
    }

    function getErrorMessage(error) {
        return error?.message || "Co loi xay ra";
    }

    function insertAtCursor(textarea, text) {
        const start = textarea.selectionStart;
        const end = textarea.selectionEnd;
        textarea.value = `${textarea.value.slice(0, start)}${text}${textarea.value.slice(end)}`;
        const next = start + text.length;
        textarea.setSelectionRange(next, next);
    }

    function escapeHtml(value) {
        return (value || "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#39;");
    }

    function isImageContent(contentType) {
        return (contentType || "").toLowerCase().startsWith("image/");
    }

    function isVideoContent(contentType) {
        return (contentType || "").toLowerCase().startsWith("video/");
    }
})();
