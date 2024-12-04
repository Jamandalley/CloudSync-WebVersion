// auth.js
document.addEventListener('DOMContentLoaded', () => {
    const authForm = document.getElementById('authForm');
    const emailInput = document.getElementById('email');
    const status = document.getElementById('status');

    authForm.addEventListener('submit', (e) => {
        e.preventDefault();
        authorizeUser();
    });

    function authorizeUser() {
        const email = emailInput.value;

        status.textContent = 'Authorizing...';
        authForm.querySelector('button').disabled = true;

        axios.post('/Home/Authorize', { email })
            .then(response => {
                if (response.data.isAuthenticated) {
                    window.location.href = '/Home/Success';
                } else {
                    window.location.href = response.data.authUrl;
                }
            })
            .catch(error => {
                console.error('Authorization error:', error);
                status.textContent = `Authorization failed: ${error.response?.data || error.message}. Please try again.`;
                authForm.querySelector('button').disabled = false;
            });
    }
});