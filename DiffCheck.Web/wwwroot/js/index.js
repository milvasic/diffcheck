(function () {
	const form = document.getElementById("compareForm");
	const leftInput = document.getElementById("leftFile");
	const rightInput = document.getElementById("rightFile");
	const MAX_FILE_SIZE_MB = (window.DiffCheckConfig && window.DiffCheckConfig.maxFileSizeMb) || 10;
	const MAX_FILE_SIZE = MAX_FILE_SIZE_MB * 1024 * 1024;
	const STORAGE_KEY_COLUMNS = "diffcheck-key-columns";
	const STORAGE_COLUMN_MAPPINGS = "diffcheck-column-mappings";

	function saveOptionsToStorage() {
		try {
			var keyEl = document.getElementById("keyColumnsRaw");
			var mapEl = document.getElementById("columnMappingsRaw");
			if (keyEl) localStorage.setItem(STORAGE_KEY_COLUMNS, keyEl.value || "");
			if (mapEl) localStorage.setItem(STORAGE_COLUMN_MAPPINGS, mapEl.value || "");
		} catch (e) {}
	}

	function restoreOptionsFromStorage() {
		try {
			var keyEl = document.getElementById("keyColumnsRaw");
			var mapEl = document.getElementById("columnMappingsRaw");
			if (keyEl) {
				var v = localStorage.getItem(STORAGE_KEY_COLUMNS);
				if (v !== null) keyEl.value = v;
			}
			if (mapEl) {
				var v = localStorage.getItem(STORAGE_COLUMN_MAPPINGS);
				if (v !== null) mapEl.value = v;
			}
		} catch (e) {}
	}

	restoreOptionsFromStorage();

	form.addEventListener("submit", function (e) {
		e.preventDefault();
	});

	function updateFileName(input, displayEl) {
		displayEl.textContent = input.files?.length
			? input.files[0].name
			: "Drop file here or click to browse";
	}

	function checkAndSubmit() {
		if (!leftInput.files?.length || !rightInput.files?.length) return;

		var errorEl = document.getElementById("compareError");
		var leftSize = leftInput.files[0].size;
		var rightSize = rightInput.files[0].size;
		if (leftSize === 0 || rightSize === 0) {
			errorEl.textContent = "One or both files are empty.";
			errorEl.classList.remove("d-none");
			return;
		}
		if (leftSize > MAX_FILE_SIZE || rightSize > MAX_FILE_SIZE) {
			errorEl.textContent = "Each file must be under " + MAX_FILE_SIZE_MB + " MB.";
			errorEl.classList.remove("d-none");
			return;
		}

		saveOptionsToStorage();
		var overlay = document.getElementById("loadingOverlay");
		var errorEl = document.getElementById("compareError");
		overlay.classList.remove("d-none");
		overlay.classList.add("d-flex");
		errorEl.classList.add("d-none");

		var formData = new FormData(form);
		var theme = document.documentElement.getAttribute("data-theme") || "light";
		var viewPref;
		try {
			viewPref = localStorage.getItem("diffcheck-view") || "table";
		} catch (e) {
			viewPref = "table";
		}

		fetch("?handler=Compare", {
			method: "POST",
			body: formData,
			headers: { "X-Theme": theme, "X-View": viewPref },
		})
			.then(function (r) {
				return r.json();
			})
			.then(function (data) {
				overlay.classList.add("d-none");
				overlay.classList.remove("d-flex");
				if (data.error) {
					errorEl.textContent = data.error;
					errorEl.classList.remove("d-none");
					return;
				}
				var container = document.getElementById("diffResultContainer");
				container.setAttribute("data-left-name", data.leftFileName || "");
				container.setAttribute("data-right-name", data.rightFileName || "");
				document.getElementById("diffReportFrame").srcdoc = data.html;
				container.classList.remove("d-none");
				document.getElementById("compareHeader").classList.add("d-none");
				document.querySelectorAll(".drop-zone").forEach(function (z) {
					z.classList.add("drop-zone-compact");
					z.querySelector(".drop-icon").setAttribute("width", "24");
					z.querySelector(".drop-icon").setAttribute("height", "24");
					z.querySelector(".drop-icon").classList.remove("mb-2");
				});
				var btn = document.getElementById("diffAddToHistoryBtn");
				if (btn && btn._origHtml) {
					btn.disabled = false;
					btn.innerHTML = btn._origHtml;
				}
			})
			.catch(function (err) {
				overlay.classList.add("d-none");
				overlay.classList.remove("d-flex");
				errorEl.textContent = "Error: " + (err.message || "Comparison failed");
				errorEl.classList.remove("d-none");
			});
	}

	document.querySelectorAll(".drop-zone").forEach((zone) => {
		const targetId = zone.dataset.target;
		const input = document.getElementById(targetId);
		const displayEl = document.getElementById(targetId + "Name");

		zone.addEventListener("click", () => input.click());

		zone.addEventListener("dragover", (e) => {
			e.preventDefault();
			zone.classList.add("drop-zone-active");
		});

		zone.addEventListener("dragleave", () => {
			zone.classList.remove("drop-zone-active");
		});

		zone.addEventListener("drop", (e) => {
			e.preventDefault();
			zone.classList.remove("drop-zone-active");
			if (e.dataTransfer.files.length) {
				input.files = e.dataTransfer.files;
				updateFileName(input, displayEl);
				checkAndSubmit();
			}
		});

		input.addEventListener("change", () => {
			updateFileName(input, displayEl);
			checkAndSubmit();
		});
	});

	// Report is generated with theme from X-Theme header, so it renders correctly from the start.
	// When user toggles theme, theme.js applies it to the iframe.

	// Fullscreen and Download for Diff Result
	const diffContainer = document.getElementById("diffResultContainer");
	if (diffContainer) {
		const expandBtn = document.getElementById("diffExpandBtn");
		const exitFsBtn = document.getElementById("diffExitFullscreenBtn");
		const downloadBtn = document.getElementById("diffDownloadBtn");
		const downloadWithDetailsBtn = document.getElementById("diffDownloadWithDetailsBtn");
		const frame = document.getElementById("diffReportFrame");

		function onFullscreenChange() {
			const isFs = document.fullscreenElement === diffContainer;
			diffContainer.classList.toggle("diff-result-fullscreen", isFs);
			expandBtn.classList.toggle("d-none", isFs);
			exitFsBtn.classList.toggle("d-none", !isFs);
		}

		expandBtn.addEventListener("click", function () {
			const el = diffContainer.requestFullscreen || diffContainer.webkitRequestFullscreen;
			if (el) el.call(diffContainer);
		});
		exitFsBtn.addEventListener("click", function () {
			const exit = document.exitFullscreen || document.webkitExitFullscreen;
			if (exit) exit.call(document);
		});
		document.addEventListener("fullscreenchange", onFullscreenChange);
		document.addEventListener("webkitfullscreenchange", onFullscreenChange);

		const downloadModal = document.getElementById("diffDownloadModal");
		const keyValuesContainer = document.getElementById("diffDownloadKeyValues");
		const addRowBtn = document.getElementById("diffDownloadAddRow");
		const downloadConfirmBtn = document.getElementById("diffDownloadConfirm");

		function escapeHtml(s) {
			var d = document.createElement("div");
			d.textContent = s;
			return d.innerHTML;
		}

		function addKeyValueRow() {
			var row = document.createElement("div");
			row.className = "row g-2 mb-2 diff-download-row";
			row.innerHTML =
				'<div class="col-4"><input type="text" class="form-control form-control-sm" placeholder="Key" /></div><div class="col"><input type="text" class="form-control form-control-sm" placeholder="Value" /></div><div class="col-auto"><button type="button" class="btn btn-outline-secondary btn-sm diff-download-remove" title="Remove row">&times;</button></div>';
			row.querySelector(".diff-download-remove").addEventListener("click", function () {
				row.remove();
			});
			keyValuesContainer.appendChild(row);
		}

		keyValuesContainer
			.querySelector(".diff-download-remove")
			.addEventListener("click", function () {
				var rows = keyValuesContainer.querySelectorAll(".diff-download-row");
				if (rows.length > 1) this.closest(".diff-download-row").remove();
			});
		addRowBtn.addEventListener("click", addKeyValueRow);

		function getReportHtml() {
			// Always prefer the original HTML generated by HtmlReportGenerator (srcdoc),
			// so downloads are based on server-rendered content rather than the live DOM.
			if (frame && frame.srcdoc) {
				return frame.srcdoc;
			}
			var doc = frame ? frame.contentDocument : null;
			if (doc) return "<!DOCTYPE html>\n" + doc.documentElement.outerHTML;
			return "";
		}

		function doDownload(html) {
			var blob = new Blob([html], { type: "text/html" });
			var url = URL.createObjectURL(blob);
			var a = document.createElement("a");
			a.href = url;
			var leftName = (diffContainer.getAttribute("data-left-name") || "left").replace(
				/\.[^.]+$/,
				"",
			);
			var rightName = (diffContainer.getAttribute("data-right-name") || "right").replace(
				/\.[^.]+$/,
				"",
			);
			var tsMatch = (html || "").match(
				/Generated:<\/strong><\/div>\s*<div[^>]*>([0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2})/,
			);
			var ts = tsMatch ? tsMatch[1].replace(" ", "_").replace(":", "-").replace(":", "") : "";
			a.download = "diff-" + leftName + "-vs-" + rightName + (ts ? "_" + ts : "") + ".html";
			a.click();
			URL.revokeObjectURL(url);
		}

		downloadBtn.addEventListener("click", function () {
			if (!frame || !frame.srcdoc) return;
			doDownload(getReportHtml());
		});

		downloadWithDetailsBtn.addEventListener("click", function () {
			if (!frame || !frame.srcdoc) return;
			keyValuesContainer.innerHTML = "";
			addKeyValueRow();
			var modal = new bootstrap.Modal(downloadModal);
			modal.show();
		});

		downloadConfirmBtn.addEventListener("click", function () {
			var pairs = [];
			keyValuesContainer.querySelectorAll(".diff-download-row").forEach(function (row) {
				var keyInput = row.querySelector('input[placeholder="Key"]');
				var valInput = row.querySelector('input[placeholder="Value"]');
				var k = keyInput && keyInput.value ? keyInput.value.trim() : "";
				var v = valInput && valInput.value ? valInput.value.trim() : "";
				if (k || v) pairs.push({ key: k || "(key)", value: v });
			});

			var html = getReportHtml();
			if (pairs.length > 0) {
				var lines = pairs.map(function (p) {
					return escapeHtml(p.key) + ": " + escapeHtml(p.value);
				});
				var customBlock =
					'        <div class="file-info custom-details">\n          <div class="file-info-block">\n            <div class="file-name"><strong>Additional details</strong></div>\n            <div class="file-stats">' +
					lines.join("<br>") +
					"</div>\n          </div>\n        </div>\n        ";
				html = html.replace(/<div class="summary">/, customBlock + '<div class="summary">');
			}
			doDownload(html);
			bootstrap.Modal.getInstance(downloadModal).hide();
		});

		// Parse summary counts from report HTML (badges: "Added: N", etc.)
		function parseSummaryFromReportHtml(html) {
			var num = function (label) {
				var m = (html || "").match(new RegExp(label + ":\\s*(\\d+)"));
				return m ? parseInt(m[1], 10) : 0;
			};
			return {
				added: num("Added"),
				removed: num("Removed"),
				modified: num("Modified"),
				reordered: num("Reordered"),
				unchanged: num("Unchanged"),
			};
		}

		// Add to history (IndexedDB, client-only)
		var addToHistoryBtn = document.getElementById("diffAddToHistoryBtn");
		if (addToHistoryBtn && typeof DiffCheckHistoryDB !== "undefined") {
			addToHistoryBtn.addEventListener("click", function () {
				if (addToHistoryBtn.disabled) return;
				var leftName = diffContainer.getAttribute("data-left-name") || "Left file";
				var rightName = diffContainer.getAttribute("data-right-name") || "Right file";
				var html = getReportHtml();
				if (!html) return;
				var run = {
					id:
						typeof crypto !== "undefined" && crypto.randomUUID
							? crypto.randomUUID()
							: "run-" + Date.now() + "-" + Math.random().toString(36).slice(2),
					leftFileName: leftName,
					rightFileName: rightName,
					createdAt: new Date().toISOString(),
					reportHtml: html,
					summary: parseSummaryFromReportHtml(html),
					tags: [],
				};
				DiffCheckHistoryDB.addRun(run)
					.then(function () {
						addToHistoryBtn.disabled = true;
						addToHistoryBtn._origHtml = addToHistoryBtn._origHtml || addToHistoryBtn.innerHTML;
						addToHistoryBtn.textContent = "Saved";
					})
					.catch(function (err) {
						alert("Could not save to history: " + (err.message || err));
					});
			});
		}
	}
})();

// Profiles
(function () {
	var profileSelect = document.getElementById("profileSelect");
	var profileApplyBtn = document.getElementById("profileApplyBtn");
	var profileDeleteBtn = document.getElementById("profileDeleteBtn");
	var profileSaveBtn = document.getElementById("profileSaveBtn");
	var profileNameInput = document.getElementById("profileNameInput");
	var profileError = document.getElementById("profileError");
	if (!profileSelect) return;

	var profileData = [];

	function getAntiforgeryToken() {
		var el = document.querySelector('input[name="__RequestVerificationToken"]');
		return el ? el.value : "";
	}

	function showProfileError(msg) {
		profileError.textContent = msg;
		profileError.classList.remove("d-none");
	}

	function clearProfileError() {
		profileError.classList.add("d-none");
	}

	function profileMappingsToRaw(mappings) {
		if (!mappings || !mappings.length) return "";
		return mappings
			.map(function (m) {
				return m.leftHeader + ":" + m.rightHeader;
			})
			.join("\n");
	}

	function profileKeyColumnsToRaw(cols) {
		if (!cols || !cols.length) return "";
		return cols.join("\n");
	}

	function loadProfiles() {
		fetch("?handler=Profiles")
			.then(function (r) {
				return r.json();
			})
			.then(function (data) {
				profileData = data || [];
				var current = profileSelect.value;
				while (profileSelect.options.length > 1) profileSelect.remove(1);
				profileData.forEach(function (p) {
					var opt = document.createElement("option");
					opt.value = p.name;
					opt.textContent = p.name;
					profileSelect.appendChild(opt);
				});
				if (
					current &&
					profileData.some(function (p) {
						return p.name === current;
					})
				) {
					profileSelect.value = current;
				}
				var hasSelection = !!profileSelect.value;
				profileApplyBtn.disabled = !hasSelection;
				profileDeleteBtn.disabled = !hasSelection;
			})
			.catch(function () {});
	}

	profileSelect.addEventListener("change", function () {
		var hasSelection = !!profileSelect.value;
		profileApplyBtn.disabled = !hasSelection;
		profileDeleteBtn.disabled = !hasSelection;
		clearProfileError();
	});

	profileApplyBtn.addEventListener("click", function () {
		var name = profileSelect.value;
		if (!name) return;
		var profile = profileData.find(function (p) {
			return p.name === name;
		});
		if (!profile) return;
		var keyEl = document.getElementById("keyColumnsRaw");
		var mapEl = document.getElementById("columnMappingsRaw");
		if (keyEl) keyEl.value = profileKeyColumnsToRaw(profile.keyColumns);
		if (mapEl) mapEl.value = profileMappingsToRaw(profile.columnMappings);
		// Expand the related sections so the user can see the applied values
		var keySection = document.getElementById("keyColumnsSection");
		var mapSection = document.getElementById("columnMappingsSection");
		if (keyEl && keyEl.value && keySection && !keySection.classList.contains("show"))
			new bootstrap.Collapse(keySection, { toggle: false }).show();
		if (mapEl && mapEl.value && mapSection && !mapSection.classList.contains("show"))
			new bootstrap.Collapse(mapSection, { toggle: false }).show();
		clearProfileError();
	});

	profileDeleteBtn.addEventListener("click", function () {
		var name = profileSelect.value;
		if (!name) return;
		if (!confirm('Delete profile "' + name + '"?')) return;
		var fd = new FormData();
		fd.append("name", name);
		fd.append("__RequestVerificationToken", getAntiforgeryToken());
		fetch("?handler=DeleteProfile", { method: "POST", body: fd })
			.then(function (r) {
				return r.json();
			})
			.then(function (data) {
				if (data.error) {
					showProfileError(data.error);
					return;
				}
				clearProfileError();
				loadProfiles();
			})
			.catch(function () {
				showProfileError("Could not delete profile.");
			});
	});

	profileSaveBtn.addEventListener("click", function () {
		var name = (profileNameInput.value || "").trim();
		if (!name) {
			showProfileError("Enter a profile name.");
			return;
		}
		var keyEl = document.getElementById("keyColumnsRaw");
		var mapEl = document.getElementById("columnMappingsRaw");
		var fd = new FormData();
		fd.append("name", name);
		fd.append("keyColumnsRaw", keyEl ? keyEl.value : "");
		fd.append("columnMappingsRaw", mapEl ? mapEl.value : "");
		fd.append("__RequestVerificationToken", getAntiforgeryToken());
		fetch("?handler=SaveProfile", { method: "POST", body: fd })
			.then(function (r) {
				return r.json();
			})
			.then(function (data) {
				if (data.error) {
					showProfileError(data.error);
					return;
				}
				clearProfileError();
				profileNameInput.value = "";
				loadProfiles();
			})
			.catch(function () {
				showProfileError("Could not save profile.");
			});
	});

	loadProfiles();
})();
