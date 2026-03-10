/**
 * IndexedDB layer for diff run history. Client-side only; no server storage.
 * Database: DiffCheckHistory, store: runs (id, leftFileName, rightFileName, createdAt, reportHtml).
 */
(function (global) {
	"use strict";

	var DB_NAME = "DiffCheckHistory";
	var STORE_NAME = "runs";
	var DB_VERSION = 1;
	var MAX_RUNS = 50;

	function openDb() {
		return new Promise(function (resolve, reject) {
			var req = indexedDB.open(DB_NAME, DB_VERSION);
			req.onerror = function () {
				reject(req.error);
			};
			req.onsuccess = function () {
				resolve(req.result);
			};
			req.onupgradeneeded = function (e) {
				var db = e.target.result;
				if (!db.objectStoreNames.contains(STORE_NAME)) {
					var store = db.createObjectStore(STORE_NAME, { keyPath: "id" });
					store.createIndex("createdAt", "createdAt", { unique: false });
				}
			};
		});
	}

	/**
	 * @param {{ id: string, leftFileName: string, rightFileName: string, createdAt: string, reportHtml: string }} run
	 * @returns {Promise<void>}
	 */
	function addRun(run) {
		return openDb().then(function (db) {
			return new Promise(function (resolve, reject) {
				var tx = db.transaction(STORE_NAME, "readwrite");
				var store = tx.objectStore(STORE_NAME);
				var index = store.index("createdAt");

				function doAdd() {
					store.add(run).onsuccess = function () {
						resolve();
					};
				}

				// Optional: keep only last MAX_RUNS
				var countReq = store.count();
				countReq.onsuccess = function () {
					var count = countReq.result;
					if (count < MAX_RUNS) {
						doAdd();
						return;
					}
					// Get oldest by ascending createdAt and delete until count < MAX_RUNS
					var cursorReq = index.openCursor();
					var toDelete = count - MAX_RUNS + 1;
					cursorReq.onsuccess = function () {
						var cursor = cursorReq.result;
						if (cursor && toDelete > 0) {
							store.delete(cursor.primaryKey);
							toDelete--;
							cursor.continue();
						} else {
							doAdd();
						}
					};
				};
				tx.onerror = function () {
					reject(tx.error);
				};
			});
		});
	}

	/**
	 * @returns {Promise<Array<{ id: string, leftFileName: string, rightFileName: string, createdAt: string }>>}
	 */
	function getAllRuns() {
		return openDb().then(function (db) {
			return new Promise(function (resolve, reject) {
				var tx = db.transaction(STORE_NAME, "readonly");
				var index = tx.objectStore(STORE_NAME).index("createdAt");
				var req = index.openCursor(null, "prev"); // newest first
				var runs = [];
				req.onsuccess = function () {
					var cursor = req.result;
					if (cursor) {
						var v = cursor.value;
						runs.push({
							id: v.id,
							leftFileName: v.leftFileName,
							rightFileName: v.rightFileName,
							createdAt: v.createdAt,
							summary: v.summary,
							tags: v.tags || [],
						});
						cursor.continue();
					} else {
						resolve(runs);
					}
				};
				tx.onerror = function () {
					reject(tx.error);
				};
			});
		});
	}

	/**
	 * @param {string} id
	 * @returns {Promise<{ id: string, leftFileName: string, rightFileName: string, createdAt: string, reportHtml: string }|undefined>}
	 */
	function getRun(id) {
		return openDb().then(function (db) {
			return new Promise(function (resolve, reject) {
				var tx = db.transaction(STORE_NAME, "readonly");
				var req = tx.objectStore(STORE_NAME).get(id);
				req.onsuccess = function () {
					resolve(req.result);
				};
				tx.onerror = function () {
					reject(tx.error);
				};
			});
		});
	}

	/**
	 * @param {string} id
	 * @param {{ tags?: string[] }} updates
	 * @returns {Promise<void>}
	 */
	function updateRun(id, updates) {
		return openDb().then(function (db) {
			return new Promise(function (resolve, reject) {
				var tx = db.transaction(STORE_NAME, "readwrite");
				var store = tx.objectStore(STORE_NAME);
				var getReq = store.get(id);
				getReq.onsuccess = function () {
					var run = getReq.result;
					if (!run) {
						resolve();
						return;
					}
					if (updates.tags !== undefined) run.tags = updates.tags;
					store.put(run).onsuccess = function () {
						resolve();
					};
				};
				tx.onerror = function () {
					reject(tx.error);
				};
			});
		});
	}

	/**
	 * @param {string} id
	 * @returns {Promise<void>}
	 */
	function deleteRun(id) {
		return openDb().then(function (db) {
			return new Promise(function (resolve, reject) {
				var tx = db.transaction(STORE_NAME, "readwrite");
				var req = tx.objectStore(STORE_NAME).delete(id);
				req.onsuccess = function () {
					resolve();
				};
				tx.onerror = function () {
					reject(tx.error);
				};
			});
		});
	}

	global.DiffCheckHistoryDB = {
		addRun: addRun,
		getAllRuns: getAllRuns,
		getRun: getRun,
		updateRun: updateRun,
		deleteRun: deleteRun,
	};
})(typeof window !== "undefined" ? window : this);
