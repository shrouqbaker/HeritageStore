/**
 * wishlist-page.js
 * إدارة وعرض صفحة المفضلة (/Favorite) للزائر والمستخدم المسجل
 */

(function () {
    'use strict';

    const wishlistGrid = document.getElementById('wishlistGrid');
    const emptyState = document.getElementById('emptyWishlistState');

    if (!wishlistGrid) return;

    function formatNumber(num) {
        if (num === null || num === undefined) return '0';
        return Number(num).toLocaleString('en-US');
    }

    // جلب بيانات المفضلة
    async function fetchFavoritesData() {
        const isAuth = typeof window.isAuthenticated !== 'undefined' && window.isAuthenticated;

        if (isAuth) {
            try {
                const response = await fetch('/Favorite/GetFavoriteData');
                return await response.json();
            } catch (err) {
                console.error('Failed to load user favorites:', err);
                return { success: false, items: [] };
            }
        } else {
            // زائر: جلب المعرفات من LocalStorage
            const guestFavs = GuestStorage.getFavorites();
            if (!guestFavs.length) {
                return { success: true, items: [], count: 0 };
            }

            try {
                const response = await fetch('/Favorite/GetGuestFavoriteData', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(guestFavs)
                });
                return await response.json();
            } catch (err) {
                console.error('Failed to load guest favorites:', err);
                return { success: false, items: [] };
            }
        }
    }

    // بناء الواجهة
    function renderFavorites(data) {
        if (!data.items || data.items.length === 0) {
            emptyState.classList.remove('d-none');
            wishlistGrid.innerHTML = '';
            return;
        }

        emptyState.classList.add('d-none');

        let rowsHtml = '';
        data.items.forEach(item => {
            let statusBadge = '';
            let actionButton = '';

            if (item.isSold) {
                statusBadge = '<span class="status-badge sold">تم البيع</span>';
                actionButton = '<button type="button" class="btn-add-to-cart disabled" disabled>مباعة</button>';
            } else if (item.listingType === 'for_sale' && item.status === 'approved') {
                statusBadge = '<span class="status-badge instock">متاح للطلب</span>';
                actionButton = `
                    <button type="button" class="btn-add-to-cart wishlist-add-cart-btn" data-product-id="${item.productId}">
                        <i class="bi bi-bag-plus me-1"></i> أضف للسلة
                    </button>
                `;
            } else {
                statusBadge = '<span class="status-badge showcase">للعرض التراثي</span>';
                actionButton = `
                    <a href="/Product/Details/${item.productId}" class="btn-add-to-cart view-only">
                        عرض التفاصيل
                    </a>
                `;
            }

            rowsHtml += `
                <tr class="wishlist-row" data-product-id="${item.productId}">
                    <td class="col-remove">
                        <button type="button" class="favorite-remove-btn" data-product-id="${item.productId}" title="إزالة من المفضلة">
                            <i class="bi bi-x-lg"></i>
                        </button>
                    </td>
                    <td>
                        <div class="product-info-cell">
                            <a href="/Product/Details/${item.productId}" class="product-img-box">
                                <img src="${item.imageUrl}" alt="${item.name}" loading="lazy" />
                            </a>
                            <div>
                                <a href="/Product/Details/${item.productId}" class="product-title-link">
                                    <div class="product-title">${item.name}</div>
                                </a>
                                <div class="product-subtext">
                                    ${item.categoryName ? item.categoryName + ' • ' : ''}${item.country || ''}
                                </div>
                            </div>
                        </div>
                    </td>
                    <td>
                        <span class="price-amount">
                            ${item.price ? formatNumber(item.price) + ' د.أ' : '—'}
                        </span>
                    </td>
                    <td>
                        <span class="embroidery-badge">
                            ${item.embroideryName ? 'تطريز ' + item.embroideryName : '—'}
                        </span>
                    </td>
                    <td>
                        ${statusBadge}
                    </td>
                    <td class="text-center">
                        ${actionButton}
                    </td>
                </tr>
            `;
        });

        wishlistGrid.innerHTML = `
            <div class="wishlist-table-wrapper">
                <table class="wishlist-table table">
                    <thead>
                        <tr>
                            <th class="col-remove"></th>
                            <th>المنتج</th>
                            <th>السعر</th>
                            <th>نوع التطريز</th>
                            <th>الحالة</th>
                            <th class="text-center">الإجراء</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${rowsHtml}
                    </tbody>
                </table>
            </div>
        `;
    }

    // إزالة عنصر من المفضلة
    async function removeFromFavorites(productId) {
        productId = parseInt(productId, 10);
        if (!productId) return;

        const isAuth = typeof window.isAuthenticated !== 'undefined' && window.isAuthenticated;
        const row = document.querySelector(`.wishlist-row[data-product-id="${productId}"]`);

        if (row) {
            row.classList.add('is-removing');
        }

        if (isAuth) {
            try {
                await fetch('/Favorite/ToggleFavorite', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ productId: productId })
                });
            } catch (err) {
                console.error('Failed to remove favorite:', err);
            }
        } else {
            GuestStorage.removeFavorite(productId);
        }

        setTimeout(async () => {
            if (row) row.remove();
            const remainingRows = document.querySelectorAll('.wishlist-row');
            if (remainingRows.length === 0) {
                emptyState.classList.remove('d-none');
                wishlistGrid.innerHTML = '';
            }
        }, 250);
    }

    // إضافة للسلة من صفحة المفضلة
    async function addToCartFromWishlist(button) {
        const productId = parseInt(button.dataset.productId, 10);
        if (!productId) return;

        const originalText = button.innerHTML;
        button.disabled = true;

        const isAuth = typeof window.isAuthenticated !== 'undefined' && window.isAuthenticated;

        if (isAuth) {
            try {
                const response = await fetch('/Cart/AddToCart', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ productId: productId })
                });
                const res = await response.json();
                if (res.success) {
                    GuestStorage.updateCartBadge(res.count);
                    button.innerHTML = '<i class="bi bi-check2 me-1"></i> في السلة ✓';
                    button.style.backgroundColor = '#2e4a3d';
                } else {
                    button.disabled = false;
                }
            } catch (e) {
                button.disabled = false;
            }
        } else {
            GuestStorage.addToCart(productId);
            button.innerHTML = '<i class="bi bi-check2 me-1"></i> في السلة ✓';
            button.style.backgroundColor = '#2e4a3d';
        }

        setTimeout(() => {
            button.disabled = false;
            button.innerHTML = originalText;
            button.style.backgroundColor = '';
        }, 2000);
    }

    // تفويض الأحداث
    wishlistGrid.addEventListener('click', function (e) {
        // حذف
        const removeBtn = e.target.closest('.favorite-remove-btn');
        if (removeBtn) {
            e.preventDefault();
            const pid = removeBtn.dataset.productId;
            removeFromFavorites(pid);
            return;
        }

        // إضافة للسلة
        const addCartBtn = e.target.closest('.wishlist-add-cart-btn');
        if (addCartBtn) {
            e.preventDefault();
            addToCartFromWishlist(addCartBtn);
            return;
        }
    });

    async function loadWishlist() {
        const data = await fetchFavoritesData();
        renderFavorites(data);
    }

    document.addEventListener('DOMContentLoaded', loadWishlist);
})();
