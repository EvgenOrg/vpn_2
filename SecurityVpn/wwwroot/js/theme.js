// wwwroot/js/theme.js
// Загружается динамически через JS isolation (JSRuntime.InvokeAsync("import", "./js/theme.js")),
// поэтому НЕ требует ручного добавления <script> в wwwroot/index.html.

export function applyTheme(theme) {
    let effective = theme;

    if (theme === 'system') {
        effective = (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches)
            ? 'dark'
            : 'light';
    }

    document.documentElement.setAttribute('data-theme', effective);
    return effective;
}

export function watchSystemTheme(dotnetRef) {
    if (!window.matchMedia) return;

    const mq = window.matchMedia('(prefers-color-scheme: dark)');
    const handler = (e) => {
        dotnetRef.invokeMethodAsync('OnSystemThemeChanged', e.matches ? 'dark' : 'light');
    };

    // addEventListener доступен в современных WebView2/WKWebView, но подстрахуемся
    if (mq.addEventListener) {
        mq.addEventListener('change', handler);
    } else if (mq.addListener) {
        mq.addListener(handler);
    }
}
