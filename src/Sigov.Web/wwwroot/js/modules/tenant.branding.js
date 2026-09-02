(() => {
    'use strict';

    document.addEventListener('DOMContentLoaded', () => {
        const form = document.getElementById('formTenantBranding');
        if (!form) return;

        const fields = {
            name: document.getElementById('NomeExibicao'),
            logoUrl: document.getElementById('LogoUrl'),
            logoFile: document.getElementById('LogoArquivo'),
            width: document.getElementById('LogoWidthPx'),
            height: document.getElementById('LogoHeightPx'),
            fit: document.getElementById('LogoFit'),
            primary: document.getElementById('CorPrimaria'),
            secondary: document.getElementById('CorSecundaria'),
            accent: document.getElementById('CorAcento')
        };

        const preview = document.getElementById('brandingPreview');
        const previewLogo = document.getElementById('brandingPreviewLogo');
        const previewName = document.getElementById('brandingPreviewName');
        const widthValue = document.getElementById('logoWidthValue');
        const heightValue = document.getElementById('logoHeightValue');
        const submitButton = document.getElementById('brandingSubmit');
        let objectUrl = null;

        const updatePreview = () => {
            const width = clampNumber(fields.width?.value, 80, 480, 220);
            const height = clampNumber(fields.height?.value, 32, 180, 72);
            const fit = fields.fit?.value || 'contain';

            if (widthValue) widthValue.textContent = `${width}px`;
            if (heightValue) heightValue.textContent = `${height}px`;

            if (preview) {
                preview.style.setProperty('--branding-primary', fields.primary?.value || '#0d6efd');
                preview.style.setProperty('--branding-secondary', fields.secondary?.value || '#6c757d');
                preview.style.setProperty('--branding-accent', fields.accent?.value || '#198754');
                preview.style.setProperty('--branding-logo-width', `${width}px`);
                preview.style.setProperty('--branding-logo-height', `${height}px`);
            }

            if (previewLogo) {
                previewLogo.style.objectFit = fit;
                const src = objectUrl || fields.logoUrl?.value || preview?.dataset.logoUrl || '';
                if (src) {
                    previewLogo.src = src;
                    previewLogo.classList.add('is-visible');
                } else {
                    previewLogo.removeAttribute('src');
                    previewLogo.classList.remove('is-visible');
                }
            }

            if (previewName) previewName.textContent = fields.name?.value || 'SIGOV PLUS';
        };

        Object.values(fields).forEach(field => field?.addEventListener('input', updatePreview));
        fields.fit?.addEventListener('change', updatePreview);

        fields.logoFile?.addEventListener('change', () => {
            if (objectUrl) URL.revokeObjectURL(objectUrl);
            const file = fields.logoFile?.files?.[0];
            objectUrl = file ? URL.createObjectURL(file) : null;
            updatePreview();
        });

        form.addEventListener('submit', async event => {
            if (form.dataset.logoPrepared === 'true') return;
            const file = fields.logoFile?.files?.[0];
            if (!file || !isRasterLogo(file)) return;

            event.preventDefault();
            submitButton?.setAttribute('disabled', 'disabled');
            submitButton?.classList.add('disabled');

            try {
                const width = clampNumber(fields.width?.value, 80, 480, 220);
                const height = clampNumber(fields.height?.value, 32, 180, 72);
                const fit = fields.fit?.value || 'contain';
                const resizedBlob = await resizeLogo(file, width, height, fit);
                const resizedName = normalizeFileName(file.name);
                const resizedFile = new File([resizedBlob], resizedName, { type: 'image/png', lastModified: Date.now() });
                const dataTransfer = new DataTransfer();
                dataTransfer.items.add(resizedFile);
                fields.logoFile.files = dataTransfer.files;
            } catch {
                // Se o navegador nao suportar canvas/DataTransfer, o servidor ainda valida e salva o arquivo original.
            }

            form.dataset.logoPrepared = 'true';
            if (typeof form.requestSubmit === 'function') form.requestSubmit(submitButton || undefined);
            else form.submit();
        });

        updatePreview();
    });

    function clampNumber(value, min, max, fallback) {
        const number = Number.parseInt(value, 10);
        if (Number.isNaN(number)) return fallback;
        return Math.min(Math.max(number, min), max);
    }

    function isRasterLogo(file) {
        return ['image/png', 'image/jpeg', 'image/webp'].includes(file.type);
    }

    function normalizeFileName(fileName) {
        const baseName = (fileName || 'tenant-logo')
            .replace(/\.[^.]+$/, '')
            .replace(/[^a-z0-9_-]+/gi, '-')
            .replace(/^-+|-+$/g, '')
            .toLowerCase() || 'tenant-logo';
        return `${baseName}-resized.png`;
    }

    function resizeLogo(file, targetWidth, targetHeight, fit) {
        return new Promise((resolve, reject) => {
            const image = new Image();
            const url = URL.createObjectURL(file);

            image.onload = () => {
                try {
                    const canvas = document.createElement('canvas');
                    canvas.width = targetWidth;
                    canvas.height = targetHeight;
                    const context = canvas.getContext('2d');
                    if (!context) throw new Error('Canvas indisponivel.');

                    context.clearRect(0, 0, targetWidth, targetHeight);
                    const rect = calculateDrawRect(image.width, image.height, targetWidth, targetHeight, fit);
                    context.drawImage(image, rect.sx, rect.sy, rect.sw, rect.sh, rect.dx, rect.dy, rect.dw, rect.dh);

                    canvas.toBlob(blob => {
                        URL.revokeObjectURL(url);
                        if (blob) resolve(blob);
                        else reject(new Error('Falha ao gerar logo redimensionada.'));
                    }, 'image/png', 0.92);
                } catch (error) {
                    URL.revokeObjectURL(url);
                    reject(error);
                }
            };

            image.onerror = () => {
                URL.revokeObjectURL(url);
                reject(new Error('Imagem invalida.'));
            };

            image.src = url;
        });
    }

    function calculateDrawRect(sourceWidth, sourceHeight, targetWidth, targetHeight, fit) {
        if (fit === 'fill') {
            return { sx: 0, sy: 0, sw: sourceWidth, sh: sourceHeight, dx: 0, dy: 0, dw: targetWidth, dh: targetHeight };
        }

        const sourceRatio = sourceWidth / sourceHeight;
        const targetRatio = targetWidth / targetHeight;

        if (fit === 'cover') {
            if (sourceRatio > targetRatio) {
                const sw = sourceHeight * targetRatio;
                return { sx: (sourceWidth - sw) / 2, sy: 0, sw, sh: sourceHeight, dx: 0, dy: 0, dw: targetWidth, dh: targetHeight };
            }

            const sh = sourceWidth / targetRatio;
            return { sx: 0, sy: (sourceHeight - sh) / 2, sw: sourceWidth, sh, dx: 0, dy: 0, dw: targetWidth, dh: targetHeight };
        }

        if (sourceRatio > targetRatio) {
            const dh = targetWidth / sourceRatio;
            return { sx: 0, sy: 0, sw: sourceWidth, sh: sourceHeight, dx: 0, dy: (targetHeight - dh) / 2, dw: targetWidth, dh };
        }

        const dw = targetHeight * sourceRatio;
        return { sx: 0, sy: 0, sw: sourceWidth, sh: sourceHeight, dx: (targetWidth - dw) / 2, dy: 0, dw, dh: targetHeight };
    }
})();
