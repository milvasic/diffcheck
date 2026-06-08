// Jobs drawer — tracks background diff jobs and surfaces them in a slide-in panel.
// Exposes window.DiffCheckJobs.addJob(jobId, label) for index.js to call after submit.
(function () {
	var POLL_INTERVAL_MS = 500;
	var MAX_CONSECUTIVE_MISSES = 6; // ~3s of 500ms polls before declaring the job lost
	var trackedJobs = {}; // jobId → { id, label, leftFileName, rightFileName, status, percent, message, error, warningMessage, html, missCount }
	var pollTimer = null;
	var drawer = null;
	var trigger = null;
	var badge = null;
	var listEl = null;
	var emptyEl = null;
	var toastContainer = null;
	var onViewJob = null; // callback set by index.js

	// ── DOM bootstrap ──────────────────────────────────────────────────────

	function init() {
		injectDrawerHtml();
		injectTriggerHtml();
		injectToastContainer();
		bindEvents();
	}

	function injectDrawerHtml() {
		drawer = document.createElement("div");
		drawer.id = "jobsDrawer";
		drawer.className = "jobs-drawer";
		drawer.setAttribute("role", "dialog");
		drawer.setAttribute("aria-label", "Background diff jobs");
		drawer.setAttribute("aria-hidden", "true");
		drawer.innerHTML =
			'<div class="jobs-drawer-header">' +
			'<span class="jobs-drawer-header-title">' +
			'<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="currentColor" viewBox="0 0 16 16" aria-hidden="true">' +
			'<path d="M6 1a1 1 0 0 0-1 1v1H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2V5a2 2 0 0 0-2-2h-1V2a1 1 0 0 0-2 0v1H6V2a1 1 0 0 0-1-1zm3 3V2H7v2h2zm-1 3a.5.5 0 0 1 .5.5v2.793l1.146 1.147a.5.5 0 0 1-.708.707L7.5 10.5v-3A.5.5 0 0 1 8 7z"/>' +
			"</svg>" +
			"Jobs" +
			"</span>" +
			'<button type="button" class="btn-close btn-sm" id="jobsDrawerClose" aria-label="Close jobs drawer"></button>' +
			"</div>" +
			'<div class="jobs-drawer-body" id="jobsDrawerList" aria-live="polite">' +
			'<div class="jobs-drawer-empty text-muted" id="jobsDrawerEmpty">No jobs yet. Submit a diff to get started.</div>' +
			"</div>";
		document.body.appendChild(drawer);
		listEl = document.getElementById("jobsDrawerList");
		emptyEl = document.getElementById("jobsDrawerEmpty");
	}

	function injectTriggerHtml() {
		trigger = document.createElement("button");
		trigger.type = "button";
		trigger.id = "jobsDrawerTrigger";
		trigger.className = "btn btn-primary jobs-drawer-trigger";
		trigger.setAttribute("aria-label", "Toggle jobs drawer");
		trigger.setAttribute("aria-expanded", "false");
		trigger.setAttribute("aria-controls", "jobsDrawer");
		trigger.innerHTML =
			'<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="currentColor" viewBox="0 0 16 16" aria-hidden="true">' +
			'<path d="M6 1a1 1 0 0 0-1 1v1H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2V5a2 2 0 0 0-2-2h-1V2a1 1 0 0 0-2 0v1H6V2a1 1 0 0 0-1-1zm3 3V2H7v2h2zm-1 3a.5.5 0 0 1 .5.5v2.793l1.146 1.147a.5.5 0 0 1-.708.707L7.5 10.5v-3A.5.5 0 0 1 8 7z"/>' +
			"</svg>" +
			'<span class="jobs-drawer-badge d-none" id="jobsDrawerBadge"></span>' +
			"Jobs";
		document.body.appendChild(trigger);
		badge = document.getElementById("jobsDrawerBadge");
	}

	function injectToastContainer() {
		toastContainer = document.createElement("div");
		toastContainer.className = "jobs-toast-container";
		toastContainer.setAttribute("aria-live", "polite");
		document.body.appendChild(toastContainer);
	}

	function bindEvents() {
		trigger.addEventListener("click", toggleDrawer);
		document.getElementById("jobsDrawerClose").addEventListener("click", closeDrawer);

		document.addEventListener("keydown", function (e) {
			var isJ = e.key === "j" || e.key === "J";
			if (isJ && (e.ctrlKey || e.metaKey) && !e.altKey) {
				e.preventDefault();
				toggleDrawer();
			}
			if (e.key === "Escape" && drawer.classList.contains("is-open")) {
				closeDrawer();
			}
		});
	}

	// ── Drawer open/close ──────────────────────────────────────────────────

	function openDrawer() {
		drawer.classList.add("is-open");
		drawer.setAttribute("aria-hidden", "false");
		trigger.setAttribute("aria-expanded", "true");
	}

	function closeDrawer() {
		drawer.classList.remove("is-open");
		drawer.setAttribute("aria-hidden", "true");
		trigger.setAttribute("aria-expanded", "false");
	}

	function toggleDrawer() {
		if (drawer.classList.contains("is-open")) {
			closeDrawer();
		} else {
			openDrawer();
		}
	}

	// ── Job tracking ───────────────────────────────────────────────────────

	function addJob(jobId, label, leftFileName, rightFileName) {
		trackedJobs[jobId] = {
			id: jobId,
			label: label,
			leftFileName: leftFileName || "",
			rightFileName: rightFileName || "",
			status: "pending",
			percent: 0,
			message: "Queued",
			error: null,
			warningMessage: null,
			html: null,
			missCount: 0,
		};
		renderList();
		openDrawer();
		startPolling();
	}

	function startPolling() {
		if (pollTimer) return;
		pollTimer = setInterval(pollActiveJobs, POLL_INTERVAL_MS);
	}

	function stopPolling() {
		if (pollTimer) {
			clearInterval(pollTimer);
			pollTimer = null;
		}
	}

	function hasActiveJobs() {
		return Object.values(trackedJobs).some(function (j) {
			return j.status === "pending" || j.status === "running";
		});
	}

	function pollActiveJobs() {
		var active = Object.values(trackedJobs).filter(function (j) {
			return j.status === "pending" || j.status === "running";
		});

		if (active.length === 0) {
			stopPolling();
			return;
		}

		active.forEach(function (job) {
			fetch("?handler=JobStatus&jobId=" + encodeURIComponent(job.id), { cache: "no-store" })
				.then(function (r) {
					return r.json();
				})
				.then(function (data) {
					var prev = trackedJobs[job.id];
					if (!prev) return;
					if (!data || !data.found) {
						prev.missCount = (prev.missCount || 0) + 1;
						if (prev.missCount >= MAX_CONSECUTIVE_MISSES) {
							trackedJobs[job.id] = Object.assign({}, prev, {
								status: "failed",
								error: "Server restarted — result lost",
								message: "Failed",
								missCount: 0,
							});
							renderList();
							showToast("✗ Diff failed: " + truncate(prev.label, 40));
							if (!hasActiveJobs()) stopPolling();
						}
						return;
					}
					trackedJobs[data.id] = {
						id: data.id,
						label: data.label || prev.label || data.id,
						leftFileName: data.leftFileName || prev.leftFileName || "",
						rightFileName: data.rightFileName || prev.rightFileName || "",
						status: data.status,
						percent: data.percent,
						message: data.message,
						error: data.error || null,
						warningMessage: data.warningMessage || null,
						html: data.html || prev.html || null,
						missCount: 0,
					};
					renderList();
					if (data.status === "done" && prev.status !== "done") {
						showToast("✓ Diff complete: " + truncate(trackedJobs[data.id].label, 40));
					} else if (data.status === "failed" && prev.status !== "failed") {
						showToast("✗ Diff failed: " + truncate(trackedJobs[data.id].label, 40));
					}
					if (!hasActiveJobs()) stopPolling();
				})
				.catch(function () {});
		});
	}

	// ── Rendering ──────────────────────────────────────────────────────────

	function renderList() {
		var jobs = Object.values(trackedJobs).sort(function (a, b) {
			return 0; // already in insertion order for modern engines; server list for re-init
		});

		// Count active for badge
		var activeCount = jobs.filter(function (j) {
			return j.status === "pending" || j.status === "running";
		}).length;

		if (badge) {
			if (activeCount > 0) {
				badge.textContent = String(activeCount);
				badge.classList.remove("d-none");
			} else {
				badge.classList.add("d-none");
			}
		}

		if (jobs.length === 0) {
			if (emptyEl) emptyEl.classList.remove("d-none");
			// Remove all job items
			listEl.querySelectorAll(".jobs-drawer-item").forEach(function (el) {
				el.remove();
			});
			return;
		}

		if (emptyEl) emptyEl.classList.add("d-none");

		// Reconcile DOM — update existing, add new, remove missing
		var existingItems = {};
		listEl.querySelectorAll(".jobs-drawer-item[data-job-id]").forEach(function (el) {
			existingItems[el.getAttribute("data-job-id")] = el;
		});

		// Remove items no longer tracked
		Object.keys(existingItems).forEach(function (id) {
			if (!trackedJobs[id]) existingItems[id].remove();
		});

		// Render jobs in order (most recent first if we reverse the array)
		var reversed = jobs.slice().reverse();
		reversed.forEach(function (job) {
			var el = existingItems[job.id];
			if (!el) {
				el = document.createElement("div");
				el.className = "jobs-drawer-item";
				el.setAttribute("data-job-id", job.id);
				listEl.insertBefore(el, listEl.firstChild);
			}
			el.innerHTML = buildJobItemHtml(job);
			var viewBtn = el.querySelector(".jobs-view-btn");
			if (viewBtn) {
				viewBtn.addEventListener("click", function () {
					handleViewJob(job.id);
				});
			}
		});
	}

	function buildJobItemHtml(job) {
		var iconHtml = statusIcon(job.status);
		var metaText = statusMeta(job);
		var progressHtml =
			job.status === "running"
				? '<div class="jobs-drawer-item-progress">' +
					'<div class="jobs-drawer-item-progress-bar" style="width:' +
					job.percent +
					'%"></div>' +
					"</div>"
				: "";
		var actionsHtml =
			job.status === "done"
				? '<div class="jobs-drawer-item-actions">' +
					'<button type="button" class="btn btn-sm btn-outline-primary py-0 px-1 jobs-view-btn" style="font-size:0.7rem">View</button>' +
					"</div>"
				: "";

		return (
			'<div class="jobs-drawer-item-icon">' +
			iconHtml +
			"</div>" +
			'<div class="jobs-drawer-item-body">' +
			'<div class="jobs-drawer-item-label" title="' +
			escapeAttr(job.label) +
			'">' +
			escapeHtml(truncate(job.label, 50)) +
			"</div>" +
			'<div class="jobs-drawer-item-meta">' +
			escapeHtml(metaText) +
			"</div>" +
			progressHtml +
			"</div>" +
			actionsHtml
		);
	}

	function statusIcon(status) {
		if (status === "done")
			return (
				'<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="#198754" viewBox="0 0 16 16" aria-label="Done">' +
				'<path d="M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14zm0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16z"/>' +
				'<path d="M10.97 4.97a.235.235 0 0 0-.02.022L7.477 9.417 5.384 7.323a.75.75 0 0 0-1.06 1.06L6.97 11.03a.75.75 0 0 0 1.079-.02l3.992-4.99a.75.75 0 0 0-1.071-1.05z"/>' +
				"</svg>"
			);
		if (status === "failed")
			return (
				'<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="#dc3545" viewBox="0 0 16 16" aria-label="Failed">' +
				'<path d="M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14zm0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16z"/>' +
				'<path d="M4.646 4.646a.5.5 0 0 1 .708 0L8 7.293l2.646-2.647a.5.5 0 0 1 .708.708L8.707 8l2.647 2.646a.5.5 0 0 1-.708.708L8 8.707l-2.646 2.647a.5.5 0 0 1-.708-.708L7.293 8 4.646 5.354a.5.5 0 0 1 0-.708z"/>' +
				"</svg>"
			);
		// pending or running: spinner
		return (
			'<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="#0d6efd" viewBox="0 0 16 16" aria-label="Running">' +
			'<path d="M8 3a5 5 0 1 0 4.546 2.914.5.5 0 0 1 .908-.417A6 6 0 1 1 8 2v1z"/>' +
			'<path d="M8 4.466V.534a.25.25 0 0 1 .41-.192l2.36 1.966c.12.1.12.284 0 .384L8.41 4.658A.25.25 0 0 1 8 4.466z"/>' +
			"</svg>"
		);
	}

	function statusMeta(job) {
		if (job.status === "done") return "Complete";
		if (job.status === "failed") return job.error ? truncate(job.error, 60) : "Failed";
		if (job.status === "running") return job.message || "Running…";
		return "Queued";
	}

	// ── View result ────────────────────────────────────────────────────────

	function handleViewJob(jobId) {
		var job = trackedJobs[jobId];
		if (!job || job.status !== "done") return;

		if (job.html) {
			dispatchViewJob(job);
			closeDrawer();
			return;
		}

		// Fetch full result (html only returned when done)
		fetch("?handler=JobStatus&jobId=" + encodeURIComponent(jobId), { cache: "no-store" })
			.then(function (r) {
				return r.json();
			})
			.then(function (data) {
				if (data && data.html) {
					trackedJobs[jobId] = Object.assign({}, trackedJobs[jobId], { html: data.html });
					dispatchViewJob(trackedJobs[jobId]);
					closeDrawer();
				}
			})
			.catch(function () {});
	}

	function dispatchViewJob(job) {
		if (typeof onViewJob === "function") {
			onViewJob(job);
			return;
		}
		// Fallback: fire custom event for index.js to handle
		var event = new CustomEvent("diffcheck:viewjob", { detail: job });
		document.dispatchEvent(event);
	}

	// ── Toast ──────────────────────────────────────────────────────────────

	function showToast(message) {
		var toast = document.createElement("div");
		toast.className = "jobs-toast";
		toast.textContent = message;
		toastContainer.appendChild(toast);

		requestAnimationFrame(function () {
			requestAnimationFrame(function () {
				toast.classList.add("is-visible");
			});
		});

		setTimeout(function () {
			toast.classList.remove("is-visible");
			setTimeout(function () {
				toast.remove();
			}, 300);
		}, 4000);
	}

	// ── Utilities ──────────────────────────────────────────────────────────

	function truncate(str, max) {
		if (!str) return "";
		return str.length > max ? str.slice(0, max - 1) + "…" : str;
	}

	function escapeHtml(str) {
		var d = document.createElement("div");
		d.textContent = str;
		return d.innerHTML;
	}

	function escapeAttr(str) {
		return escapeHtml(str).replace(/"/g, "&quot;");
	}

	// ── Public API ─────────────────────────────────────────────────────────

	window.DiffCheckJobs = {
		addJob: function (jobId, label, leftFileName, rightFileName) {
			addJob(jobId, label, leftFileName, rightFileName);
		},
		setOnViewJob: function (cb) {
			onViewJob = cb;
		},
		openDrawer: openDrawer,
		closeDrawer: closeDrawer,
	};

	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", init);
	} else {
		init();
	}
})();
