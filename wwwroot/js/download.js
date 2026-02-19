window.downloadWithProgress = async (url, fileName, dotNetHelper) => {
    const response = await fetch(url);
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
            await dotNetHelper.invokeMethodAsync('OnDownloadProgress', receivedLength, contentLength);
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
    window.URL.revokeObjectURL(downloadUrl);
};

window.triggerFileDownload = (url, fileName) => {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName || '';
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
};
