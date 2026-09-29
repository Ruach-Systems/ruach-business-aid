window.mashalUi = {
  scrollAppToTop() {
    requestAnimationFrame(() => requestAnimationFrame(() => {
      const shell = document.querySelector('.app-shell');
      if (shell) {
        shell.scrollTop = 0;
        shell.scrollLeft = 0;
      }
    }));
  }
};
