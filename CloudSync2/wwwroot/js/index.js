document.addEventListener('DOMContentLoaded', () => {
    const dropZone = document.getElementById('dropZone');
    const fileInput = document.getElementById('fileInput');
    const uploadButton = document.getElementById('uploadButton');
    const status = document.getElementById('status');
    const fileList = document.getElementById('fileList');
    
    let files = [];

    function updateFileList() {
        fileList.innerHTML = '';
        files.forEach((file, index) => {
            const fileItem = document.createElement('div');
            fileItem.className = 'file-item fade-in';
            fileItem.innerHTML = `
                <span>${file.name}</span>
                <span class="remove-file" data-index="${index}">&#10005;</span>
            `;
            fileList.appendChild(fileItem);
        });

        uploadButton.disabled = files.length === 0;
    }

    function addFiles(newFiles) {
        files = [...files, ...newFiles];
        updateFileList();
    }

    dropZone.addEventListener('dragover', (e) => {
        e.preventDefault();
        dropZone.classList.add('dragover');
    });

    dropZone.addEventListener('dragleave', () => {
        dropZone.classList.remove('dragover');
    });

    dropZone.addEventListener('drop', (e) => {
        e.preventDefault();
        dropZone.classList.remove('dragover');
        addFiles(Array.from(e.dataTransfer.files));
    });

    fileInput.addEventListener('change', () => {
        addFiles(Array.from(fileInput.files));
    });

    fileList.addEventListener('click', (e) => {
        if (e.target.classList.contains('remove-file')) {
            const index = parseInt(e.target.dataset.index);
            files.splice(index, 1);
            updateFileList();
        }
    });

    uploadButton.addEventListener('click', uploadFiles);
    
    function uploadFiles() {
        const formData = new FormData();
        files.forEach(file => {
            formData.append('files', file);
        });

        status.textContent = 'Uploading...';
        uploadButton.disabled = true;

        axios.post('/Home/Upload', formData, {
            headers: {
                'Content-Type': 'multipart/form-data'
            }
        })
        .then(response => {
            status.textContent = 'Upload successful. Redirecting to authentication page...';
            window.location.href = '/Home/Auth';
        })
        .catch(error => {
            status.textContent = 'Upload failed. Please try again.';
            uploadButton.disabled = false;
        });
    }
});
