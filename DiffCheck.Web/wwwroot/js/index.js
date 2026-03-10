(function () {
	const form = document.getElementById("compareForm");
	const leftInput = document.getElementById("leftFile");
	const rightInput = document.getElementById("rightFile");
	const MAX_FILE_SIZE_MB = (window.DiffCheckConfig && window.DiffCheckConfig.maxFileSizeMb) || 10;
	const MAX_FILE_SIZE = MAX_FILE_SIZE_MB * 1024 * 1024;

	const STORAGE_KEY_COLUMNS = "diffcheck-key-columns";
	const STORAGE_COLUMN_MAPPINGS = "diffcheck-column-mappings";
	const STORAGE_CASE_INSENSITIVE = "diffcheck-case-insensitive";
	const STORAGE_TRIM_WHITESPACE = "diffcheck-trim-whitespace";
	const STORAGE_NUMERIC_TOLERANCE = "diffcheck-numeric-tolerance";
	const STORAGE_MATCH_THRESHOLD = "diffcheck-match-threshold";

	// ── Chip Input ──────────────────────────────────────────────────────

	function ChipInput(container, hiddenInput, opts) {
		opts = opts || {};
		this.container = container;
		this.hiddenInput = hiddenInput;
		this.input = container.querySelector(".chip-input-field");
		this.chips = [];
		this.paired = !!opts.paired;
		var self = this;

		container.addEventListener("click", function () {
			self.input.focus();
		});

		this.input.addEventListener("keydown", function (e) {
			if (e.key === "Enter" || e.key === ",") {
				e.preventDefault();
				var val = self.input.value.trim();
				if (val) self.addRaw(val);
				self.input.value = "";
			} else if (e.key === "Backspace" && !self.input.value && self.chips.length) {
				self.removeIndex(self.chips.length - 1);
			}
		});

		this.input.addEventListener("paste", function (e) {
			e.preventDefault();
			var text = (e.clipboardData || window.clipboardData).getData("text") || "";
			var parts = text.split(/[,\n\r]+/);
			parts.forEach(function (p) {
				var v = p.trim();
				if (v) self.addRaw(v);
			});
			self.input.value = "";
		});

		// Pre-populate from hidden input
		this._initFromHidden();
	}

	ChipInput.prototype._initFromHidden = function () {
		var val = this.hiddenInput.value || "";
		if (!val.trim()) return;
		var self = this;
		if (this.paired) {
			val.split("\n").forEach(function (line) {
				var t = line.trim();
				if (t) self.addRaw(t);
			});
		} else {
			val.split(/[,\n\r]+/).forEach(function (v) {
				var t = v.trim();
				if (t) self.addRaw(t);
			});
		}
	};

	ChipInput.prototype.addRaw = function (raw) {
		if (this.paired) {
			var sep = raw.indexOf(":") >= 0 ? ":" : ",";
			var idx = raw.indexOf(sep);
			if (idx < 0) return;
			var left = raw.substring(0, idx).trim();
			var right = raw.substring(idx + 1).trim();
			if (!left || !right) return;
			this._addChip({ left: left, right: right, display: left + " \u2192 " + right });
		} else {
			var v = raw.trim();
			if (!v) return;
			this._addChip({ value: v, display: v });
		}
	};

	ChipInput.prototype._addChip = function (data) {
		var self = this;
		var chip = document.createElement("span");
		chip.className = "chip";
		chip.textContent = data.display;
		var close = document.createElement("button");
		close.type = "button";
		close.className = "chip-close";
		close.innerHTML = "&times;";
		close.addEventListener("click", function (e) {
			e.stopPropagation();
			var i = self.chips.indexOf(data);
			if (i >= 0) self.removeIndex(i);
		});
		chip.appendChild(close);
		data._el = chip;
		this.chips.push(data);
		this.container.insertBefore(chip, this.input);
		this._sync();
	};

	ChipInput.prototype.removeIndex = function (i) {
		var data = this.chips[i];
		if (!data) return;
		if (data._el && data._el.parentNode) data._el.parentNode.removeChild(data._el);
		this.chips.splice(i, 1);
		this._sync();
	};

	ChipInput.prototype._sync = function () {
		if (this.paired) {
			this.hiddenInput.value = this.chips
				.map(function (c) {
					return c.left + ":" + c.right;
				})
				.join("\n");
		} else {
			this.hiddenInput.value = this.chips
				.map(function (c) {
					return c.value;
				})
				.join(",");
		}
	};

	ChipInput.prototype.setChips = function (rawValues) {
		// Clear all existing chips
		var self = this;
		while (this.chips.length) this.removeIndex(this.chips.length - 1);
		(rawValues || []).forEach(function (v) {
			self.addRaw(v);
		});
	};

	ChipInput.prototype.getValues = function () {
		if (this.paired) {
			return this.chips.map(function (c) {
				return c.left + ":" + c.right;
			});
		}
		return this.chips.map(function (c) {
			return c.value;
		});
	};

	// Initialize chip inputs
	var keyColumnsChip = new ChipInput(
		document.getElementById("keyColumnsChipContainer"),
		document.getElementById("keyColumnsRaw"),
	);

	var columnMappingsChip = new ChipInput(
		document.getElementById("columnMappingsChipContainer"),
		document.getElementById("columnMappingsRaw"),
		{ paired: true },
	);

	// Expose chip inputs for the profiles IIFE
	window._diffcheckKeyColumnsChip = keyColumnsChip;
	window._diffcheckColumnMappingsChip = columnMappingsChip;

	// ── Save / Restore Options from localStorage ────────────────────────

	function saveOptionsToStorage() {
		try {
			localStorage.setItem(
				STORAGE_KEY_COLUMNS,
				document.getElementById("keyColumnsRaw").value || "",
			);
			localStorage.setItem(
				STORAGE_COLUMN_MAPPINGS,
				document.getElementById("columnMappingsRaw").value || "",
			);
			var ci = document.getElementById("caseInsensitive");
			var tw = document.getElementById("trimWhitespace");
			var nt = document.getElementById("numericToleranceRaw");
			var mt = document.getElementById("matchThresholdRaw");
			if (ci) localStorage.setItem(STORAGE_CASE_INSENSITIVE, ci.checked ? "1" : "0");
			if (tw) localStorage.setItem(STORAGE_TRIM_WHITESPACE, tw.checked ? "1" : "0");
			if (nt) localStorage.setItem(STORAGE_NUMERIC_TOLERANCE, nt.value || "");
			if (mt) localStorage.setItem(STORAGE_MATCH_THRESHOLD, mt.value || "");
		} catch (e) {}
	}

	function restoreOptionsFromStorage() {
		try {
			var keyVal = localStorage.getItem(STORAGE_KEY_COLUMNS);
			var mapVal = localStorage.getItem(STORAGE_COLUMN_MAPPINGS);
			if (keyVal !== null) {
				document.getElementById("keyColumnsRaw").value = keyVal;
				keyColumnsChip.setChips(
					keyVal.split(/[,\n\r]+/).filter(function (v) {
						return v.trim();
					}),
				);
			}
			if (mapVal !== null) {
				document.getElementById("columnMappingsRaw").value = mapVal;
				columnMappingsChip.setChips(
					mapVal.split("\n").filter(function (v) {
						return v.trim();
					}),
				);
			}
			var ci = localStorage.getItem(STORAGE_CASE_INSENSITIVE);
			var tw = localStorage.getItem(STORAGE_TRIM_WHITESPACE);
			var nt = localStorage.getItem(STORAGE_NUMERIC_TOLERANCE);
			var mt = localStorage.getItem(STORAGE_MATCH_THRESHOLD);
			if (ci !== null) document.getElementById("caseInsensitive").checked = ci === "1";
			if (tw !== null) document.getElementById("trimWhitespace").checked = tw === "1";
			if (nt !== null) document.getElementById("numericToleranceRaw").value = nt;
			if (mt !== null) document.getElementById("matchThresholdRaw").value = mt;
		} catch (e) {}
	}

	restoreOptionsFromStorage();

	// ── Form submit / file drop ─────────────────────────────────────────

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
				var settingsBar = document.getElementById("settingsBar");
				if (settingsBar) settingsBar.classList.add("settings-bar-compact");
				var rerunBtn = document.getElementById("rerunBtn");
				if (rerunBtn) rerunBtn.classList.remove("d-none");
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

	// Rerun button
	var rerunBtn = document.getElementById("rerunBtn");
	if (rerunBtn) {
		rerunBtn.addEventListener("click", function () {
			checkAndSubmit();
		});
	}

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
				profileDeleteBtn.disabled = !profileSelect.value;
			})
			.catch(function () {});
	}

	// Auto-apply on selection change
	profileSelect.addEventListener("change", function () {
		var name = profileSelect.value;
		profileDeleteBtn.disabled = !name;
		clearProfileError();
		if (!name) return;
		var profile = profileData.find(function (p) {
			return p.name === name;
		});
		if (!profile) return;

		// Key columns
		var keyCols = profile.keyColumns || [];
		window._diffcheckKeyColumnsChip.setChips(keyCols);

		// Column mappings
		var maps = (profile.columnMappings || []).map(function (m) {
			return m.leftHeader + ":" + m.rightHeader;
		});
		window._diffcheckColumnMappingsChip.setChips(maps);

		// Normalization options
		var opts = profile.options;
		var ci = document.getElementById("caseInsensitive");
		var tw = document.getElementById("trimWhitespace");
		var nt = document.getElementById("numericToleranceRaw");
		var mt = document.getElementById("matchThresholdRaw");
		if (opts) {
			if (ci) ci.checked = !opts.caseSensitive;
			if (tw) tw.checked = !!opts.trimWhitespace;
			if (nt) nt.value = opts.numericTolerance != null ? opts.numericTolerance : "";
			if (mt)
				mt.value =
					opts.matchThreshold != null && opts.matchThreshold !== 0.5 ? opts.matchThreshold : "";
		} else {
			if (ci) ci.checked = false;
			if (tw) tw.checked = false;
			if (nt) nt.value = "";
			if (mt) mt.value = "";
		}
	});

	// Toggle profile name input visibility on save button
	profileSaveBtn.addEventListener("click", function () {
		if (profileNameInput.classList.contains("d-none")) {
			profileNameInput.classList.remove("d-none");
			profileNameInput.focus();
			return;
		}
		var name = (profileNameInput.value || "").trim();
		if (!name) {
			showProfileError("Enter a profile name.");
			return;
		}
		var fd = new FormData();
		fd.append("name", name);
		fd.append("keyColumnsRaw", document.getElementById("keyColumnsRaw").value || "");
		fd.append("columnMappingsRaw", document.getElementById("columnMappingsRaw").value || "");
		var ci = document.getElementById("caseInsensitive");
		var tw = document.getElementById("trimWhitespace");
		var nt = document.getElementById("numericToleranceRaw");
		var mt = document.getElementById("matchThresholdRaw");
		if (ci && ci.checked) fd.append("caseInsensitive", "true");
		if (tw && tw.checked) fd.append("trimWhitespace", "true");
		if (nt && nt.value) fd.append("numericToleranceRaw", nt.value);
		if (mt && mt.value) fd.append("matchThresholdRaw", mt.value);
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
				profileNameInput.classList.add("d-none");
				loadProfiles();
			})
			.catch(function () {
				showProfileError("Could not save profile.");
			});
	});

	profileNameInput.addEventListener("keydown", function (e) {
		if (e.key === "Enter") {
			e.preventDefault();
			profileSaveBtn.click();
		} else if (e.key === "Escape") {
			profileNameInput.classList.add("d-none");
			profileNameInput.value = "";
		}
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

	loadProfiles();
})();
