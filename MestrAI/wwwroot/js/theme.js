// Theme Management for Dark/Light Mode
(function() {
    'use strict';

    // Theme constants
    const THEME_KEY = 'rpg-session-manager-theme';
    const THEMES = {
        LIGHT: 'light',
        DARK: 'dark',
        AUTO: 'auto'
    };

    // Get system preference
    function getSystemTheme() {
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? THEMES.DARK : THEMES.LIGHT;
    }

    // Get stored theme preference
    function getStoredTheme() {
        return localStorage.getItem(THEME_KEY) || THEMES.AUTO;
    }

    // Store theme preference
    function setStoredTheme(theme) {
        localStorage.setItem(THEME_KEY, theme);
    }

    // Apply theme to document
    function applyTheme(theme) {
        let actualTheme = theme;
        
        if (theme === THEMES.AUTO) {
            actualTheme = getSystemTheme();
        }
        
        document.documentElement.setAttribute('data-bs-theme', actualTheme);
        
        // Update theme toggle button icon
        updateThemeToggleIcon(theme);
        
        // Dispatch custom event for other components
        window.dispatchEvent(new CustomEvent('themeChanged', { 
            detail: { theme: actualTheme, preference: theme } 
        }));
    }

    // Update theme toggle button icon
    function updateThemeToggleIcon(currentTheme) {
        const toggleBtn = document.getElementById('theme-toggle');
        if (!toggleBtn) return;

        const icon = toggleBtn.querySelector('i') || toggleBtn.querySelector('span');
        if (!icon) return;

        // Remove existing classes
        icon.className = '';
        
        // Add appropriate icon class based on current theme
        switch (currentTheme) {
            case THEMES.LIGHT:
                icon.className = 'bi bi-sun-fill';
                toggleBtn.setAttribute('aria-label', 'Alternar para modo escuro');
                toggleBtn.setAttribute('title', 'Alternar para modo escuro');
                break;
            case THEMES.DARK:
                icon.className = 'bi bi-moon-fill';
                toggleBtn.setAttribute('aria-label', 'Alternar para modo claro');
                toggleBtn.setAttribute('title', 'Alternar para modo claro');
                break;
            case THEMES.AUTO:
                icon.className = 'bi bi-circle-half';
                toggleBtn.setAttribute('aria-label', 'Alternar para modo automático');
                toggleBtn.setAttribute('title', 'Seguir preferência do sistema');
                break;
        }
    }

    // Cycle through themes
    function cycleTheme() {
        const currentTheme = getStoredTheme();
        let nextTheme;

        switch (currentTheme) {
            case THEMES.LIGHT:
                nextTheme = THEMES.DARK;
                break;
            case THEMES.DARK:
                nextTheme = THEMES.AUTO;
                break;
            case THEMES.AUTO:
            default:
                nextTheme = THEMES.LIGHT;
                break;
        }

        setStoredTheme(nextTheme);
        applyTheme(nextTheme);
        
        // Announce theme change to screen readers
        announceThemeChange(nextTheme);
    }

    // Announce theme change for accessibility
    function announceThemeChange(theme) {
        const announcement = document.getElementById('theme-announcement');
        if (!announcement) return;

        let message;
        switch (theme) {
            case THEMES.LIGHT:
                message = 'Modo claro ativado';
                break;
            case THEMES.DARK:
                message = 'Modo escuro ativado';
                break;
            case THEMES.AUTO:
                message = 'Modo automático ativado - seguindo preferência do sistema';
                break;
        }

        announcement.textContent = message;
        
        // Clear announcement after a short delay
        setTimeout(() => {
            announcement.textContent = '';
        }, 1000);
    }

    // Initialize theme system
    function initTheme() {
        // Create theme toggle button
        createThemeToggle();
        
        // Create announcement area for screen readers
        createAnnouncementArea();
        
        // Apply initial theme
        const storedTheme = getStoredTheme();
        applyTheme(storedTheme);
        
        // Listen for system theme changes
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function() {
            const currentPreference = getStoredTheme();
            if (currentPreference === THEMES.AUTO) {
                applyTheme(THEMES.AUTO);
            }
        });
    }

    // Create theme toggle button
    function createThemeToggle() {
        // Check if button already exists
        if (document.getElementById('theme-toggle')) return;

        const toggleBtn = document.createElement('button');
        toggleBtn.id = 'theme-toggle';
        toggleBtn.className = 'theme-toggle btn';
        toggleBtn.setAttribute('type', 'button');
        toggleBtn.setAttribute('aria-label', 'Alternar tema');
        toggleBtn.setAttribute('title', 'Alternar tema');
        
        const icon = document.createElement('i');
        toggleBtn.appendChild(icon);
        
        toggleBtn.addEventListener('click', cycleTheme);
        
        // Add keyboard support
        toggleBtn.addEventListener('keydown', function(e) {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                cycleTheme();
            }
        });
        
        document.body.appendChild(toggleBtn);
    }

    // Create announcement area for screen readers
    function createAnnouncementArea() {
        if (document.getElementById('theme-announcement')) return;

        const announcement = document.createElement('div');
        announcement.id = 'theme-announcement';
        announcement.setAttribute('aria-live', 'polite');
        announcement.setAttribute('aria-atomic', 'true');
        announcement.className = 'visually-hidden';
        
        document.body.appendChild(announcement);
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initTheme);
    } else {
        initTheme();
    }

    // Export functions for external use
    window.ThemeManager = {
        getTheme: getStoredTheme,
        setTheme: function(theme) {
            if (Object.values(THEMES).includes(theme)) {
                setStoredTheme(theme);
                applyTheme(theme);
            }
        },
        cycleTheme: cycleTheme,
        THEMES: THEMES
    };

})();

