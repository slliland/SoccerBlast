window.soccerBlastMaps = (function () {
  var maps = new Map();

  function initVenueMap(elementId, lat, lng, venueName) {
    var el = document.getElementById(elementId);
    if (!el || !window.maplibregl) return;

    // Clean up existing map instance for this element
    if (maps.has(elementId)) {
      try { maps.get(elementId).remove(); } catch (e) {}
      maps.delete(elementId);
    }

    var map = new maplibregl.Map({
      container: elementId,
      style: "https://tiles.openfreemap.org/styles/liberty",
      center: [lng, lat],
      zoom: 14, // direct target zoom (no animation)
      attributionControl: true
    });

    maps.set(elementId, map);

    map.addControl(new maplibregl.NavigationControl(), "top-right");

    // Custom marker
    var markerEl = document.createElement("div");
    markerEl.style.width = "46px";
    markerEl.style.height = "46px";
    markerEl.style.borderRadius = "50%";
    markerEl.style.background = "rgba(255,255,255,0.95)";
    markerEl.style.boxShadow = "0 6px 18px rgba(0,0,0,0.25)";
    markerEl.style.display = "flex";
    markerEl.style.alignItems = "center";
    markerEl.style.justifyContent = "center";
    markerEl.style.border = "2px solid rgba(245,158,11,0.9)";

    var img = document.createElement("img");
    img.src = "/images/teams/arena.png";
    img.alt = venueName || "Venue";
    img.style.width = "28px";
    img.style.height = "28px";
    img.style.objectFit = "contain";
    markerEl.appendChild(img);

    var popupHtml =
      "<div style=\"font-weight:700; font-size:13px; padding:2px 0;\">" +
      (venueName || "Venue") +
      "</div>" +
      "<div style=\"font-size:12px; color:#6b7280;\">" +
      lat.toFixed(5) + ", " + lng.toFixed(5) +
      "</div>";

    new maplibregl.Marker({ element: markerEl, anchor: "bottom" })
      .setLngLat([lng, lat])
      .setPopup(new maplibregl.Popup({ offset: 18 }).setHTML(popupHtml))
      .addTo(map);

    map.on("load", function () {
      map.resize();
    });

    // Extra resize calls help when the map is inside tabs/collapsible content
    setTimeout(function () { map.resize(); }, 300);
    setTimeout(function () { map.resize(); }, 1000);
  }

  function resizeVenueMap(elementId) {
    var map = maps.get(elementId);
    if (!map) return;
    map.resize();
  }

  function disposeVenueMap(elementId) {
    var map = maps.get(elementId);
    if (!map) return;
    try { map.remove(); } catch (e) {}
    maps.delete(elementId);
  }

  return {
    initVenueMap: initVenueMap,
    resizeVenueMap: resizeVenueMap,
    disposeVenueMap: disposeVenueMap
  };
})();