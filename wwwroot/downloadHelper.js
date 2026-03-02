<script>
    window.downloadFile = function (fileName, base64String, mimeType) {
        const byteCharacters = atob(base64String);
    const byteNumbers = new Array(byteCharacters.length);

    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
        }

    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], {type: mimeType });

    const link = document.createElement("a");
    link.href = window.URL.createObjectURL(blob);
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    };
</script>