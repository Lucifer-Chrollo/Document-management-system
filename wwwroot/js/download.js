window.downloadWithProgress = async (url, fileName, dotNetHelper) => {
    console.log('[DMS Download] Starting download:', { url, fileName });

    // First, track progress using fetch
    try {
        const response = await fetch(url, { credentials: 'include' });

        if (!response.ok) {
            console.error("[DMS Download] Failed:", response.status, response.statusText);
            throw new Error("Download failed: " + response.statusText);
        }

        const reader = response.body.getReader();
        const contentLength = +response.headers.get('Content-Length');

        let receivedLength = 0;

        // Read through the stream just for progress tracking
        while (true) {
            const { done, value } = await reader.read();
            if (done) break;
            receivedLength += value.length;

            if (dotNetHelper) {
                await dotNetHelper.invokeMethodAsync('OnDownloadProgress', receivedLength, contentLength || 0);
            }
        }
    } catch (e) {
        console.warn('[DMS Download] Progress tracking failed, continuing with direct download:', e);
    }

    // Use a hidden iframe to trigger the actual download — this lets the browser
    // use the server's Content-Disposition header for the filename instead of
    // generating a GUID from a blob URL.
    console.log('[DMS Download] Triggering download via iframe for:', fileName);
    let iframe = document.getElementById('dms-download-iframe');
    if (!iframe) {
        iframe = document.createElement('iframe');
        iframe.id = 'dms-download-iframe';
        iframe.style.display = 'none';
        document.body.appendChild(iframe);
    }
    iframe.src = url;

    console.log('[DMS Download] Download triggered for:', fileName);
};

window.triggerFileDownload = (url, fileName) => {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName || '';
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
};
