/**
 * product-card-actions.js
 * إدارة الضغط على أزرار السلة والمفضلة في جميع الصفحات (الرئيسية، الاستكشاف، وتفاصيل المنتج)
 * يدعم المستخدم الزائر (عبر GuestStorage) والمستخدم المسجل (عبر AJAX)
 */

(function () {
    'use strict';

    // إشعار عائم بسيط (Toast) لتحسين تجربة المستخدم
    function showNotification(message, type = 'success') {
        let toastContainer = document.getElementById('heritageToastContainer');
        if (!toastContainer) {
            toastContainer = document.createElement('div');
            toastContainer.id = 'heritageToastContainer';
            toastContainer.style.position = 'fixed';
            toastContainer.style.bottom = '24px';
            toastContainer.style.left = '24px';
            toastContainer.style.zIndex = '9999';
            toastContainer.style.display = 'flex';
            toastContainer.style.flexDirection = 'column';
            toastContainer.style.gap = '10px';
            document.body.appendChild(toastContainer);
        }

        const toast = document.createElement('div');
        toast.className = 'heritage-toast animate__animated animate__fadeInUp';
        toast.style.backgroundColor = type === 'success' ? '#2e4a3d' : '#8a3a33';
        toast.style.color = '#fff';
        toast.style.padding = '12px 20px';
        toast.style.borderRadius = '8px';
        toast.style.boxShadow = '0 6px 20px rgba(0,0,0,0.15)';
        toast.style.fontFamily = 'var(--font-body, system-ui)';
        toast.style.fontSize = '0.92rem';
        toast.style.display = 'flex';
        toast.style.alignItems = 'center';
        toast.style.gap = '10px';
        toast.style.direction = 'rtl';
        toast.style.transition = 'all 0.3s ease';

        const icon = type === 'success' ? '<i class="bi bi-check-circle-fill"></i>' : '<i class="bi bi-exclamation-circle-fill"></i>';
        toast.innerHTML = `${icon}<span>${message}</span>`;

        toastContainer.appendChild(toast);

        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateY(10px)';
            setTimeout(() => toast.remove(), 300);
        }, 2600);
    }

    // ===== معالجة إضافة المنتج للسلة =====
    async function handleAddToCart(button) {
        const productId = parseInt(button.dataset.productId, 10);
        if (!productId) return;

        // حالة تحميل بسيطة
        const originalHtml = button.innerHTML;
        button.disabled = true;

        const isAuth = typeof window.isAuthenticated !== 'undefined' && window.isAuthenticated;

        if (isAuth) {
            try {
                const response = await fetch('/Cart/AddToCart', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ productId: productId })
                });
                const data = await response.json();
                if (data.success) {
                    GuestStorage.updateCartBadge(data.count);
                    showNotification(data.message || 'تمت إضافة القطعة إلى السلة');
                    setButtonAddedState(button, originalHtml);
                } else {
                    showNotification(data.message || 'تعذر إضافة القطعة للسلة', 'error');
                    button.disabled = false;
                    button.innerHTML = originalHtml;
                }
            } catch (err) {
                console.error('Add to cart failed:', err);
                showNotification('حدث خطأ أثناء الاتصال بالخادم', 'error');
                button.disabled = false;
                button.innerHTML = originalHtml;
            }
        } else {
            // زائر
            GuestStorage.addToCart(productId);
            showNotification('تمت إضافة القطعة إلى سلتك');
            setButtonAddedState(button, originalHtml);
        }
    }

    function setButtonAddedState(button, originalHtml) {
        button.innerHTML = '<i class="bi bi-check2"></i> <span>تمت الإضافة ✓</span>';
        button.classList.add('btn-added');

        setTimeout(() => {
            button.innerHTML = originalHtml;
            button.disabled = false;
            button.classList.remove('btn-added');
        }, 1800);
    }

    // ===== معالجة تبديل المفضلة =====
    async function handleToggleFavorite(button) {
        const productId = parseInt(button.dataset.productId, 10);
        if (!productId) return;

        const isAuth = typeof window.isAuthenticated !== 'undefined' && window.isAuthenticated;

        if (isAuth) {
            try {
                const response = await fetch('/Favorite/ToggleFavorite', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ productId: productId })
                });
                const data = await response.json();
                if (data.success) {
                    updateFavoriteButtonUI(button, data.isFavorited);
                    showNotification(data.message);
                }
            } catch (err) {
                console.error('Toggle favorite failed:', err);
                showNotification('حدث خطأ أثناء التحديث', 'error');
            }
        } else {
            // زائر
            const isFavorited = GuestStorage.toggleFavorite(productId);
            updateFavoriteButtonUI(button, isFavorited);
            showNotification(isFavorited ? 'تمت إضافة القطعة إلى المفضلة' : 'تمت إزالة القطعة من المفضلة');
        }
    }

    function updateFavoriteButtonUI(button, isFavorited) {
        // دعم زر كارت المنتج (.favorite-btn) وزر صفحة التفاصيل (.wishlist)
        if (button.classList.contains('wishlist')) {
            button.classList.toggle('active', isFavorited);
            button.setAttribute('aria-pressed', isFavorited ? 'true' : 'false');
            const icon = button.querySelector('i');
            if (icon) {
                if (isFavorited) {
                    icon.classList.remove('bi-heart');
                    icon.classList.add('bi-heart-fill');
                } else {
                    icon.classList.remove('bi-heart-fill');
                    icon.classList.add('bi-heart');
                }
            }
        } else {
            button.classList.toggle('is-favorited', isFavorited);
            const icon = button.querySelector('i');
            if (icon) {
                if (isFavorited) {
                    icon.classList.remove('bi-heart');
                    icon.classList.add('bi-heart-fill');
                } else {
                    icon.classList.remove('bi-heart-fill');
                    icon.classList.add('bi-heart');
                }
            }
        }
    }

    // تهيئة حالة الأزرار عند فتح الصفحة
    async function initButtonsState() {
        const isAuth = typeof window.isAuthenticated !== 'undefined' && window.isAuthenticated;

        if (!isAuth) {
            // للزائر: مزامنة المفضلة من LocalStorage
            const guestFavs = GuestStorage.getFavorites();
            document.querySelectorAll('.favorite-btn, .wishlist').forEach(btn => {
                const pid = parseInt(btn.dataset.productId, 10);
                if (guestFavs.includes(pid)) {
                    updateFavoriteButtonUI(btn, true);
                }
            });
        } else {
            // للمسجل: لو الصفحة ما فيها FavoriteIds (مثل الرئيسية)، نجيبها ونحدث الأزرار
            try {
                const response = await fetch('/Favorite/GetFavoriteIds');
                const data = await response.json();
                if (data.favoriteIds && Array.isArray(data.favoriteIds)) {
                    document.querySelectorAll('.favorite-btn, .wishlist').forEach(btn => {
                        const pid = parseInt(btn.dataset.productId, 10);
                        if (data.favoriteIds.includes(pid)) {
                            updateFavoriteButtonUI(btn, true);
                        }
                    });
                }
            } catch (e) {
                // تجاهل بصمت
            }
        }
    }

    // تفويض الأحداث (Event Delegation) للأزرار
    document.addEventListener('click', function (e) {
        // زر السلة في الكروت (.cart-btn) أو في صفحة التفاصيل (.add-to-cart-btn)
        const cartBtn = e.target.closest('.cart-btn:not(.sold), .add-to-cart-btn');
        if (cartBtn) {
            e.preventDefault();
            handleAddToCart(cartBtn);
            return;
        }

        // زر المفضلة في الكروت (.favorite-btn) أو في صفحة التفاصيل (.wishlist)
        const favBtn = e.target.closest('.favorite-btn, .wishlist');
        if (favBtn) {
            e.preventDefault();
            e.stopPropagation();
            handleToggleFavorite(favBtn);
            return;
        }
    });

    // تشغيل التهيئة عند اكتمال تحميل الصفحة
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initButtonsState);
    } else {
        initButtonsState();
    }
})();
