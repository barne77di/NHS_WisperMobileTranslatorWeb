// chat.js — mobile-safe bindings + iOS guards

(() => {
    // Single global conversation id
    window.conversationId = window.conversationId || crypto.randomUUID();

    const $ = (id) => document.getElementById(id);
    const convo = () => $("conversation");

    function addBubble(text, role, extraHtml = "") {
        const div = document.createElement("div");
        div.className = "bubble " + (role === "me" ? "me" : "you");
        div.innerHTML = text + (extraHtml || "");
        convo()?.appendChild(div);
        if (convo()) convo().scrollTop = convo().scrollHeight;
    }
    function addMeta(html) {
        const div = document.createElement("div");
        div.className = "bubble meta";
        div.innerHTML = html;
        convo()?.appendChild(div);
        if (convo()) convo().scrollTop = convo().scrollHeight;
    }
    function escapeHtml(s) {
        return (s || "").toString().replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    // Expose helpers for inline history loader
    window.addBubble = window.addBubble || addBubble;
    window.addMeta = window.addMeta || addMeta;
    window.escapeHtml = window.escapeHtml || escapeHtml;

    // --- Recorder state
    let mediaRecorder = null;
    let chunks = [];
    let isRecording = false;
    let activeMode = null; // "listen" | "reply"
    let stopTimer = null;
    const MAX_SECONDS = 60;

    function pickMime() {
        const can = (t) => (window.MediaRecorder && MediaRecorder.isTypeSupported && MediaRecorder.isTypeSupported(t));
        if (can("audio/webm;codecs=opus")) return "audio/webm;codecs=opus";
        if (can("audio/webm")) return "audio/webm";
        return "";
    }

    async function ensureRecorder() {
        if (!navigator.mediaDevices?.getUserMedia) {
            addMeta("🎤 Microphone not available in this browser.");
            throw new Error("getUserMedia unsupported");
        }
        if (typeof MediaRecorder === "undefined") {
            addMeta("🎤 Recording not supported on this browser (no MediaRecorder). Try the latest Safari/Chrome.");
            throw new Error("MediaRecorder unsupported");
        }
        if (mediaRecorder) return;

        let stream;
        try {
            stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        } catch (e) {
            addMeta("🚫 Microphone permission denied.");
            throw e;
        }

        const mime = pickMime();
        try {
            mediaRecorder = new MediaRecorder(stream, mime ? { mimeType: mime } : undefined);
        } catch {
            mediaRecorder = new MediaRecorder(stream);
        }

        mediaRecorder.ondataavailable = (e) => { if (e.data && e.data.size > 0) chunks.push(e.data); };
        mediaRecorder.onstop = async () => {
            const blobType = mediaRecorder.mimeType || "audio/webm";
            const blob = new Blob(chunks, { type: blobType });
            chunks = [];
            setUi(false);

            if (!blob.size) { addMeta("No audio captured."); return; }

            try {
                //addMeta(`Uploading (${activeMode}): ${blob.type}, ${(blob.size / 1024).toFixed(1)} KB`);
                if (activeMode === "listen") await sendAudioForListen(blob);
                else if (activeMode === "reply") await sendAudioForReply(blob);
            } catch (e) {
                addMeta(`Upload failed: ${e?.message || e}`);
            } finally {
                activeMode = null;
            }
        };
    }

    function setUi(active) {
        isRecording = active;
        const listenBtn = $("btnRecord");
        const replyBtn = $("btnReplyRecord");

        if (listenBtn) {
            const listening = active && activeMode === "listen";
            listenBtn.classList.toggle("recording", listening);
            listenBtn.setAttribute("aria-pressed", listening ? "true" : "false");
            listenBtn.textContent = listening ? "Stop listening ⏹️" : "Start listening 🎙️";
        }
        if (replyBtn) {
            const replying = active && activeMode === "reply";
            replyBtn.classList.toggle("recording", replying);
            replyBtn.setAttribute("aria-pressed", replying ? "true" : "false");
            replyBtn.textContent = replying ? "Stop reply ⏹️" : "Speak reply 🎤";
        }
    }

    function startRecording(mode) {
        activeMode = mode;
        chunks = [];
        mediaRecorder.start();
        setUi(true);
        stopTimer = setTimeout(() => {
            if (isRecording && mediaRecorder && mediaRecorder.state !== "inactive") mediaRecorder.stop();
        }, MAX_SECONDS * 1000);
    }

    function stopRecording() {
        if (stopTimer) { clearTimeout(stopTimer); stopTimer = null; }
        if (mediaRecorder && mediaRecorder.state !== "inactive") mediaRecorder.stop();
    }

    async function fetchJson(url, opts) {
        const res = await fetch(url, opts);
        if (!res.ok) {
            let msg = `${res.status} ${res.statusText}`;
            try { const b = await res.json(); msg = b.detail || b.title || msg; } catch { }
            throw new Error(msg);
        }
        return res.json();
    }

    // --- Audio helpers
    function buildPlayableUrl(dataUrl) {
        try {
            const [meta, b64] = dataUrl.split(",");
            const mime = (meta.match(/^data:([^;]+);/) || [, "audio/mpeg"])[1];
            const bin = atob(b64);
            const bytes = new Uint8Array(bin.length);
            for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
            const blobUrl = URL.createObjectURL(new Blob([bytes], { type: mime }));
            return { blobUrl, rawUrl: dataUrl, mime };
        } catch {
            return { blobUrl: null, rawUrl: dataUrl, mime: "audio/mpeg" };
        }
    }

    function renderAudioPlayer(containerEl, url, explicitType = "audio/mpeg") {
        const wrap = document.createElement("div");
        wrap.style.marginTop = "6px";

        const audio = document.createElement("audio");
        audio.controls = true;
        audio.preload = "metadata";

        const src = document.createElement("source");
        src.src = url;
        if (explicitType) src.type = explicitType;
        audio.appendChild(src);

        audio.addEventListener("error", () => {
            const code = audio.error && audio.error.code;
            addMeta(`🔇 Audio error code ${code || "?"} (1=aborted,2=network,3=decode,4=src not supported)`);
        });

        audio.addEventListener("loadedmetadata", () => {
           // addMeta(`▶️ Audio ready (${isFinite(audio.duration) ? audio.duration.toFixed(1) : "?"}s)`);
        });

        wrap.appendChild(audio);
        containerEl.appendChild(wrap);
        try { audio.load(); } catch { }
    }

    // --- API calls
    async function sendAudioForListen(blob) {
        addMeta("Transcribing…");
        const form = new FormData();
        form.append("conversationId", window.conversationId);
        form.append("audio", blob, "speech.webm");

        const data = await fetchJson("/api/transcribe", { method: "POST", body: form });

        if (data.conversationId) window.conversationId = data.conversationId;
        const flag = flagForLang(data.detectedLanguage);
        addBubble(`${escapeHtml(data.text)}`, "you", `
      <div class="meta-row">
        <small>➡️ <strong>${escapeHtml(data.translated)}</strong></small>
        <img class="flag" src="${flag}" alt="Detected language flag"/>
      </div>`);
        addMeta(`Detected: ${(data.detectedLanguage || "auto").toUpperCase()} → EN`);
    }

    async function sendTextReply(inputText) {
        const text = (inputText || "").trim();
        if (!text) return;
        addBubble(escapeHtml(text), "me");

        const form = new FormData();
        form.append("conversationId", window.conversationId || crypto.randomUUID());
        form.append("text", text);

        const data = await fetchJson("/api/reply", { method: "POST", body: form });

        const div = document.createElement("div");
        div.className = "bubble you";
        div.innerHTML = `🔁 ${escapeHtml(data.translated || "")}`;
        convo()?.appendChild(div);

        //if (data.ttsSource) addMeta(`(tts: ${data.ttsSource})`);
        //if (data.ttsErr) addMeta(`(tts warn: ${escapeHtml(data.ttsErr)})`);
        //if (typeof data.audioLength === "number") addMeta(`(audio bytes: ${data.audioLength})`);

        let url = null, raw = null;
        const ctype = data.audioContentType || "audio/mpeg";
        if (data.audioBase64) { const u = buildPlayableUrl(data.audioBase64); url = u.blobUrl; raw = u.rawUrl; }
        if (data.audioUrl && !url) url = data.audioUrl;

        if (url) {
            renderAudioPlayer(div, url, ctype);
            // Fallback to raw data URL if blob URL fails with code 4
            setTimeout(() => {
                const aud = div.querySelector("audio");
                if (aud && aud.error && aud.error.code === 4 && raw) {
                    const fallback = document.createElement("audio");
                    fallback.controls = true; fallback.preload = "metadata";
                    const s = document.createElement("source"); s.src = raw; s.type = ctype; fallback.appendChild(s);
                    div.appendChild(fallback);
                    try { fallback.load(); } catch { }
                }
            }, 250);
        } else {
            addMeta("🔇 No audio returned for this reply.");
        }
    }

    async function sendAudioForReply(blob) {
        addMeta("Transcribing your reply…");
        const form = new FormData();
        form.append("conversationId", window.conversationId);
        form.append("audio", blob, "reply.webm");

        // ✅ Use `data` here (was `data2` in some versions, caused “data is not defined”)
        const data = await fetchJson("/api/reply-voice", { method: "POST", body: form });

        addBubble(`(You said) ${escapeHtml(data.text || "")}`, "me");

        const div = document.createElement("div");
        div.className = "bubble you";
        div.innerHTML = `🔁 ${escapeHtml(data.translated || "")}`;
        convo()?.appendChild(div);

        //if (data.ttsSource) addMeta(`(tts: ${data.ttsSource})`);
        //if (data.ttsErr) addMeta(`(tts warn: ${escapeHtml(data.ttsErr)})`);
        //if (typeof data.audioLength === "number") addMeta(`(audio bytes: ${data.audioLength})`);

        let url = null, raw = null;
        const ctype = data.audioContentType || "audio/mpeg";
        if (data.audioBase64) { const u = buildPlayableUrl(data.audioBase64); url = u.blobUrl; raw = u.rawUrl; }
        if (data.audioUrl && !url) url = data.audioUrl;

        if (url) {
            renderAudioPlayer(div, url, ctype);
            setTimeout(() => {
                const aud = div.querySelector("audio");
                if (aud && aud.error && aud.error.code === 4 && raw) {
                    const fallback = document.createElement("audio");
                    fallback.controls = true; fallback.preload = "metadata";
                    const s = document.createElement("source"); s.src = raw; s.type = ctype; fallback.appendChild(s);
                    div.appendChild(fallback);
                    try { fallback.load(); } catch { }
                }
            }, 250);
        } else {
            addMeta("🔇 No audio returned for this voice reply.");
        }
    }

    function flagForLang(lang) {
        const map = {
            "fr": "https://flagcdn.com/w20/fr.png", "es": "https://flagcdn.com/w20/es.png", "de": "https://flagcdn.com/w20/de.png",
            "it": "https://flagcdn.com/w20/it.png", "pt": "https://flagcdn.com/w20/pt.png", "pl": "https://flagcdn.com/w20/pl.png",
            "ru": "https://flagcdn.com/w20/ru.png", "tr": "https://flagcdn.com/w20/tr.png", "ar": "https://flagcdn.com/w20/sa.png",
            "zh-Hans": "https://flagcdn.com/w20/cn.png", "zh-Hant": "https://flagcdn.com/w20/tw.png", "ja": "https://flagcdn.com/w20/jp.png",
            "ko": "https://flagcdn.com/w20/kr.png", "nl": "https://flagcdn.com/w20/nl.png", "sv": "https://flagcdn.com/w20/se.png",
            "en": "https://flagcdn.com/w20/gb.png"
        };
        return map[lang] || "https://flagcdn.com/w20/un.png";
    }

    // Expose text-reply for any inline buttons
    window.sendTextReply = window.sendTextReply || sendTextReply;

    // --- Bind after DOM is ready
    document.addEventListener("DOMContentLoaded", () => {
        const listenBtn = $("btnRecord");
        const replyBtn = $("btnReplyRecord");

        if (!listenBtn && !replyBtn) return; // not the chat page

        listenBtn?.addEventListener("click", async () => {
            try {
                const isSecure = location.protocol === "https:" || location.hostname === "localhost";
                if (!isSecure) { addMeta("🔒 Microphone requires HTTPS or localhost when testing on a phone."); return; }
                await ensureRecorder();
                if (!isRecording) startRecording("listen"); else stopRecording();
            } catch (e) { console.error(e); }
        });

        replyBtn?.addEventListener("click", async () => {
            try {
                const isSecure = location.protocol === "https:" || location.hostname === "localhost";
                if (!isSecure) { addMeta("🔒 Microphone requires HTTPS or localhost when testing on a phone."); return; }
                await ensureRecorder();
                if (!isRecording) startRecording("reply"); else stopRecording();
            } catch (e) { console.error(e); }
        });
    });
})();
