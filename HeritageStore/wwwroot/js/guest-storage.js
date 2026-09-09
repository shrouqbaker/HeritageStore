/**
 * guest-storage.js
 * إدارة التخزين المحلي (LocalStorage) للزائر في السلة والمفضلة
 */

const GuestStorage = {
    CART_KEY: 'guestCart',
    FAVORITES_KEY: 'guestFavorites',

    // ===== السلة (Cart) =====
    getCart: function () {
        try {
            const data = localStorage.getItem(this.CART_KEY);
            return data ? JSON.parse(data) : [];
        } catch (e) {
            console.error('Error reading guestCart:', e);
            return [];
        }
    },

    addToCart: function (productId) {
        productId = parseInt(productId, 10);
        if (isNaN(productId) || productId <= 0) return false;

        const cart = this.getCart();
        if (!cart.includes(productId)) {
            cart.push(productId);
            try {
                localStorage.setItem(this.CART_KEY, JSON.stringify(cart));
            } catch (e) {
                console.error('Error saving guestCart:', e);
            }
        }
        this.updateCartBadge(cart.length);
        window.dispatchEvent(new CustomEvent('cart:updated', { detail: { count: cart.length } }));
        return true;
    },

    removeFromCart: function (productId) {
        productId = parseInt(productId, 10);
        const cart = this.getCart();
        const updated = cart.filter(id => id !== productId);
        try {
            localStorage.setItem(this.CART_KEY, JSON.stringify(updated));
        } catch (e) {
            console.error('Error saving guestCart:', e);
        }
        this.updateCartBadge(updated.length);
        window.dispatchEvent(new CustomEvent('cart:updated', { detail: { count: updated.length } }));
        return true;
    },

    isInCart: function (productId) {
        productId = parseInt(productId, 10);
        return this.getCart().includes(productId);
    },

    // ===== المفضلة (Favorites) =====
    getFavorites: function () {
        try {
            const data = localStorage.getItem(this.FAVORITES_KEY);
            return data ? JSON.parse(data) : [];
        } catch (e) {
            console.error('Error reading guestFavorites:', e);
            return [];
        }
    },

    addFavorite: function (productId) {
        productId = parseInt(productId, 10);
        if (isNaN(productId) || productId <= 0) return false;

        const favs = this.getFavorites();
        if (!favs.includes(productId)) {
            favs.push(productId);
            try {
                localStorage.setItem(this.FAVORITES_KEY, JSON.stringify(favs));
            } catch (e) {
                console.error('Error saving guestFavorites:', e);
            }
        }
        window.dispatchEvent(new CustomEvent('favorites:updated', { detail: { count: favs.length } }));
        return true;
    },

    removeFavorite: function (productId) {
        productId = parseInt(productId, 10);
        const favs = this.getFavorites();
        const updated = favs.filter(id => id !== productId);
        try {
            localStorage.setItem(this.FAVORITES_KEY, JSON.stringify(updated));
        } catch (e) {
            console.error('Error saving guestFavorites:', e);
        }
        window.dispatchEvent(new CustomEvent('favorites:updated', { detail: { count: updated.length } }));
        return true;
    },

    toggleFavorite: function (productId) {
        productId = parseInt(productId, 10);
        if (this.isFavorite(productId)) {
            this.removeFavorite(productId);
            return false; // أصبحت غير مفضلة
        } else {
            this.addFavorite(productId);
            return true; // أصبحت مفضلة
        }
    },

    isFavorite: function (productId) {
        productId = parseInt(productId, 10);
        return this.getFavorites().includes(productId);
    },

    // ===== تحديث شارة عداد السلة في الهيدر =====
    updateCartBadge: function (count) {
        const wrappers = document.querySelectorAll('.cart-icon-wrapper');
        wrappers.forEach(wrapper => {
            let badge = wrapper.querySelector('.cart-badge');
            if (count > 0) {
                if (!badge) {
                    badge = document.createElement('span');
                    badge.className = 'cart-badge';
                    wrapper.appendChild(badge);
                }
                badge.textContent = count;
                badge.style.display = '';
            } else {
                if (badge) {
                    badge.remove();
                }
            }
        });
    }
};

// عند تحميل الصفحة، إذا كان المستخدم زائرًا، حدّث عداد السلة من التخزين المحلي
document.addEventListener('DOMContentLoaded', function () {
    if (typeof window.isAuthenticated !== 'undefined' && !window.isAuthenticated) {
        const guestCart = GuestStorage.getCart();
        GuestStorage.updateCartBadge(guestCart.length);
    }
});
