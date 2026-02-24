window.downloadWithProgress = async (url, fileName, dotNetHelper) => {
    // Include credentials so the [Authorize] API controller doesn't block the request
    const response = await fetch(url, { credentials: 'include' });

    if (!response.ok) {
        console.error("Download failed:", response.status, response.statusText);
        throw new Error("Download failed: " + response.statusText);
    }

    const reader = response.body.getReader();
    const contentLength = +response.headers.get('Content-Length');

    let receivedLength = 0;
    let chunks = [];

    while (true) {
        const { done, value } = await reader.read();

        if (done) {
            break;
        }

        chunks.push(value);
        receivedLength += value.length;

        // Report progress back to .NET
        if (dotNetHelper) {
            await dotNetHelper.invokeMethodAsync('OnDownloadProgress', receivedLength, contentLength || 0);
        }
    }

    const blob = new Blob(chunks);
    const downloadUrl = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = downloadUrl;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();

    // Generous delay before revoking to ensure the browser has time to initiate the download 
    // with the custom filename before the blob URL is garbage collected.
    setTimeout(() => {
        window.URL.revokeObjectURL(downloadUrl);
    }, 2000);
};

window.triggerFileDownload = (url, fileName) => {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName || '';
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
};
