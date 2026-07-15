mergeInto(LibraryManager.library, {

  // Яндекс.Метрика: ym(COUNTER_ID, 'reachGoal', name, params). Пока COUNTER_ID не
  // задан в index.html (== 0), события уходят в console.log вместо отправки.
  // window.__lastLevel — читает index.html для 'session_end' на pagehide.
  YM_Event: function (namePtr, jsonPtr) {
    var name = UTF8ToString(namePtr);
    var json = UTF8ToString(jsonPtr);
    var params = {};
    try { params = JSON.parse(json) || {}; } catch (e) { }
    try {
      if (typeof params.level !== 'undefined') window.__lastLevel = params.level;
    } catch (e) { }
    try {
      if (window.YM_COUNTER_ID && typeof ym === 'function') {
        ym(window.YM_COUNTER_ID, 'reachGoal', name, params);
      } else {
        console.log('[YM stub]', name, json);
      }
    } catch (e) { console.warn('[YM] reachGoal failed', e); }
  }

});
