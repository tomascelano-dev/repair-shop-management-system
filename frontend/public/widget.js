/* RepairShop · widget de seguimiento de reparaciones.
   Uso: <script src="https://TU_DOMINIO/widget.js" data-shop="tu-taller" async></script> */
(function () {
  var script = document.currentScript;
  if (!script) return;
  var shop = script.getAttribute("data-shop");
  if (!shop) return;
  var origin = new URL(script.src).origin;
  var frame = document.createElement("iframe");
  frame.src = origin + "/seguimiento/" + encodeURIComponent(shop) + "?embed=1";
  frame.title = "Seguimiento de reparación";
  frame.loading = "lazy";
  frame.style.cssText = "width:100%;max-width:520px;height:" + (script.getAttribute("data-height") || "460") + "px;border:0;border-radius:14px;";
  script.parentNode.insertBefore(frame, script.nextSibling);
})();
