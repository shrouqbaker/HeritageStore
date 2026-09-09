/**
 * cart-page.js
 * إدارة وعرض صفحة سلة المشتريات (/Cart) للزائر والمستخدم المسجل
 */

(function () {
    'use strict';

    const cartGrid = document.getElementById('cartGrid');
    const cartEmptyState = document.getElementById('cartEmptyState');

    if (!cartGrid) return;

    function formatNumber(num) {
        if (num === null || num === undefined) return '0';
        return Number(num).toLocaleString('en-US');
    }

    // جلب بيانات السلة
    async function fetchCartData() {
        const isAuth = typeof window.isAuthenticated !== 'undefined' && window.isAuthenticated;

        if (isAuth) {
            try {
                const response = await fetch('/Cart/GetCartData');
                return await response.json();
            } catch (err) {
                console.error('Failed to load user cart:', err);
                return { success: false, items: [] };
            }
        } else {
            // زائر: جلب المعرفات من LocalStorage
            const guestCart = GuestStorage.getCart();
            if (!guestCart.length) {
                return { success: true, items: [], subtotal: 0, deliveryCost: 3, total: 0, count: 0 };
            }

            try {
                const response = await fetch('/Cart/GetGuestCartData', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(guestCart)
                });
                return await response.json();
            } catch (err) {
                console.error('Failed to load guest cart:', err);
                return { success: false, items: [] };
            }
        }
    }

    // بناء الواجهة
    function renderCart(data) {
        if (!data.items || data.items.length === 0) {
            cartEmptyState.classList.remove('d-none');
            cartGrid.innerHTML = '';
            return;
        }

        cartEmptyState.classList.add('d-none');

        const isAuth = typeof window.isAuthenticated !== 'undefined' && window.isAuthenticated;
        const hasSoldItems = data.items.some(i => i.isSold);
        const availableItems = data.items.filter(i => i.isAvailable);
        const hasAvailableItems = availableItems.length > 0;

        // رابط إتمام الطلب: إذا مسجل يذهب للـ Checkout مباشرة، إذا زائر يذهب لتسجيل الدخول ثم العودة للـ Checkout
        const checkoutUrl = isAuth
            ? '/Orders/Checkout'
            : '/Identity/Account/Login?returnUrl=' + encodeURIComponent('/Orders/Checkout');

        let itemsHtml = '';
        data.items.forEach(item => {
            itemsHtml += `
                <div class="cart-item-row ${item.isSold ? 'is-sold' : ''}" data-product-id="${item.productId}">
                    <a href="/Product/Details/${item.productId}" class="cart-item-media">
                        <img src="${item.imageUrl}" alt="${item.name}" loading="lazy" />
                    </a>
                    <div class="cart-item-info">
                        ${item.isSold ? `
                            <div class="cart-item-sold-badge">
                                <i class="bi bi-exclamation-triangle"></i> بيعت القطعة
                            </div>
                        ` : ''}
                        <a href="/Product/Details/${item.productId}" class="cart-item-title">
                            ${item.name}
                        </a>
                        <p class="cart-item-meta">
                            ${item.categoryName ? item.categoryName + ' • ' : ''}${item.country || ''}
                        </p>
                        <div class="cart-item-price">
                            ${formatNumber(item.price)} د.أ
                        </div>
                    </div>
                    <div class="cart-remove-form">
                        <button type="button" class="cart-remove-btn" data-product-id="${item.productId}" title="حذف من السلة">
                            <i class="bi bi-trash3"></i>
                        </button>
                    </div>
                </div>
            `;
        });

        const deliveryFormatted = hasAvailableItems ? formatNumber(data.deliveryCost) : '0';

        cartGrid.innerHTML = `
            <div class="row g-4 g-lg-5">
                <div class="col-lg-8">
                    <div class="cart-items-list">
                        ${itemsHtml}
                    </div>
                </div>
                <div class="col-lg-4">
                    <div class="cart-summary-panel">
                        <h2 class="cart-summary-title">ملخص الطلب</h2>
                        <div class="cart-summary-row">
                            <span>المجموع الفرعي</span>
                            <span>${formatNumber(data.subtotal)} د.أ</span>
                        </div>
                        <div class="cart-summary-row">
                            <span>رسوم التوصيل</span>
                            <span>${deliveryFormatted} د.أ</span>
                        </div>
                        <div class="cart-summary-row cart-summary-total">
                            <span>المجموع الكلي</span>
                            <span>${formatNumber(data.total)} د.أ</span>
                        </div>

                        ${hasSoldItems ? `
                            <div class="cart-sold-notice">
                                <i class="bi bi-info-circle-fill flex-shrink-0"></i>
                                <span>بعض القطع في سلتك تم بيعها ولن يتم تضمينها في الطلب.</span>
                            </div>
                        ` : ''}

                        <a href="${checkoutUrl}" class="btn-heritage w-100 text-center d-block mt-3 ${!hasAvailableItems ? 'disabled' : ''}">
                            ${isAuth ? 'متابعة الشراء' : 'تسجيل الدخول لإتمام الطلب'}
                        </a>

                        <a href="/Explore" class="btn-outline-heritage w-100 text-center d-block mt-2">
                            استكشاف المزيد
                        </a>
                    </div>
                </div>
            </div>
        `;
    }

    // حذف عنصر من السلة
    async function removeItem(productId) {
        productId = parseInt(productId, 10);
        if (!productId) return;

        const isAuth = typeof window.isAuthenticated !== 'undefined' && window.isAuthenticated;

        // تأثير بصري للحذف
        const row = document.querySelector(`.cart-item-row[data-product-id="${productId}"]`);
        if (row) {
            row.style.opacity = '0.4';
            row.style.pointerEvents = 'none';
        }

        if (isAuth) {
            try {
                const response = await fetch('/Cart/RemoveFromCart', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ productId: productId })
                });
                const res = await response.json();
                if (res.success) {
                    GuestStorage.updateCartBadge(res.count);
                    await reloadCart();
                }
            } catch (e) {
                console.error('Failed to remove item:', e);
                if (row) {
                    row.style.opacity = '1';
                    row.style.pointerEvents = '';
                }
            }
        } else {
            GuestStorage.removeFromCart(productId);
            await reloadCart();
        }
    }

    async function reloadCart() {
        const data = await fetchCartData();
        renderCart(data);
    }

    // تفويض حدث الحذف
    cartGrid.addEventListener('click', function (e) {
        const removeBtn = e.target.closest('.cart-remove-btn');
        if (removeBtn) {
            e.preventDefault();
            const productId = removeBtn.dataset.productId;
            removeItem(productId);
        }
    });

    // تحميل السلة فور جاهزية الصفحة
    document.addEventListener('DOMContentLoaded', reloadCart);
})();
