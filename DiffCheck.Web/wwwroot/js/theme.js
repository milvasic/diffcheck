(function () {
	const STORAGE_KEY = 'theme';

	function getTheme() {
		return document.documentElement.getAttribute('data-theme') || 'light';
	}

	function setTheme(theme) {
		document.documentElement.setAttribute('data-theme', theme);
		localStorage.setItem(STORAGE_KEY, theme);
		updateToggleIcon(theme);
	}

	function updateToggleIcon(theme) {
		const lightIcon = document.getElementById('themeIconLight');
		const darkIcon = document.getElementById('themeIconDark');
		if (lightIcon && darkIcon) {
			// Show sun when dark (click to switch to light), moon when light (click to switch to dark)
			if (theme === 'dark') {
				lightIcon.classList.remove('d-none');
				darkIcon.classList.add('d-none');
			} else {
				lightIcon.classList.add('d-none');
				darkIcon.classList.remove('d-none');
			}
		}
	}

	function toggleTheme() {
		const current = getTheme();
		const next = current === 'dark' ? 'light' : 'dark';
		setTheme(next);
		applyThemeToReport();
	}

	function applyThemeToReport() {
		const theme = getTheme();
		const diffFrame = document.getElementById('diffReportFrame');
		if (diffFrame && diffFrame.contentDocument) {
			diffFrame.contentDocument.documentElement.setAttribute('data-theme', theme);
		}
		const historyFrame = document.getElementById('history-report-frame');
		if (historyFrame && historyFrame.contentDocument) {
			historyFrame.contentDocument.documentElement.setAttribute('data-theme', theme);
		}
	}

	document.addEventListener('DOMContentLoaded', function () {
		updateToggleIcon(getTheme());

		const toggle = document.getElementById('themeToggle');
		if (toggle) {
			toggle.addEventListener('click', toggleTheme);
		}
	});
})();
