// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0
//
// Thin MapLibre layer. It knows markers, not entities: it reports clicks
// and moves to Blazor and draws what Blazor tells it to.

const forceColours = { 1: '#3b82f6', 2: '#ef4444', 3: '#22c55e' };

let map = null;
let dotnet = null;
let basemaps = [];
let followIntervalMs = 100;
let following = null;
let lastFollowSend = 0;
let followPending = null;
let followTimer = null;
const markers = new Map();

function styleFor(basemap) {
  if (basemap.kind === 'style') return basemap.url;
  if (basemap.kind === 'raster') {
    return {
      version: 8,
      sources: { base: { type: 'raster', tiles: basemap.tiles, tileSize: 256, attribution: basemap.attribution ?? '' } },
      layers: [{ id: 'base', type: 'raster', source: 'base' }],
    };
  }
  return { version: 8, sources: {}, layers: [{ id: 'background', type: 'background', paint: { 'background-color': basemap.color } }] };
}

function sendFollowMove() {
  followTimer = null;
  if (following === null || followPending === null) return;
  lastFollowSend = performance.now();
  dotnet.invokeMethodAsync('OnMarkerMoved', following, followPending.lat, followPending.lng, true);
  followPending = null;
}

function endFollow(report) {
  if (following === null) return;
  clearTimeout(followTimer);
  followTimer = null;
  followPending = null;
  const id = following;
  const marker = markers.get(id);
  following = null;
  map.getCanvas().style.cursor = '';
  if (!marker) return;
  marker.getElement().style.pointerEvents = '';
  marker.setDraggable(true);
  if (report) {
    const p = marker.getLngLat();
    dotnet.invokeMethodAsync('OnFollowEnded', id, p.lat, p.lng);
  }
}

function onKeyDown(e) {
  if (e.key === 'Escape') endFollow(true);
}

export function init(element, dotnetRef, options) {
  dotnet = dotnetRef;
  basemaps = options.basemaps;
  followIntervalMs = 1000 / options.followMaxUpdatesPerSecond;
  map = new maplibregl.Map({
    container: element,
    style: styleFor(basemaps[0]),
    center: [options.longitude, options.latitude],
    zoom: options.zoom,
  });
  map.addControl(new maplibregl.NavigationControl(), 'top-left');

  map.on('click', e => {
    if (following !== null) {
      markers.get(following)?.setLngLat(e.lngLat);
      endFollow(true);
      return;
    }
    dotnet.invokeMethodAsync('OnMapClick', e.lngLat.lat, e.lngLat.lng);
  });

  map.on('mousemove', e => {
    if (following === null) return;
    markers.get(following)?.setLngLat(e.lngLat);
    followPending = e.lngLat;
    // Throttled, but the last position of a burst is sent late rather than
    // dropped: where the mouse stops is where the object has to end up.
    const wait = followIntervalMs - (performance.now() - lastFollowSend);
    if (wait <= 0) sendFollowMove();
    else if (followTimer === null) followTimer = setTimeout(sendFollowMove, wait);
  });

  window.addEventListener('keydown', onKeyDown);
}

export function setBasemap(id) {
  const basemap = basemaps.find(b => b.id === id);
  // Markers are DOM elements on top of the map and survive a style change.
  if (basemap) map.setStyle(styleFor(basemap));
}

export function upsertMarker(m) {
  let marker = markers.get(m.id);
  if (!marker) {
    const el = document.createElement('div');
    el.className = 'entity-marker';
    el.innerHTML = '<span class="dot"></span><span class="label"></span>';
    el.addEventListener('click', ev => {
      ev.stopPropagation();
      dotnet.invokeMethodAsync('OnMarkerSelected', m.id);
    });
    marker = new maplibregl.Marker({ element: el, draggable: true, anchor: 'left' })
      .setLngLat([m.longitude, m.latitude])
      .addTo(map);
    marker.on('dragend', () => {
      const p = marker.getLngLat();
      dotnet.invokeMethodAsync('OnMarkerMoved', m.id, p.lat, p.lng, false);
    });
    markers.set(m.id, marker);
  }
  const el = marker.getElement();
  el.querySelector('.dot').style.background = forceColours[m.force] ?? '#888';
  el.querySelector('.label').textContent = m.name;
  el.classList.toggle('selected', m.selected);
  // While following, the mouse owns the position; Blazor's echo lags behind it.
  if (following !== m.id) marker.setLngLat([m.longitude, m.latitude]);
}

export function removeMarker(id) {
  if (following === id) endFollow(false);
  markers.get(id)?.remove();
  markers.delete(id);
}

export function startFollow(id) {
  endFollow(true);
  const marker = markers.get(id);
  if (!marker) return;
  following = id;
  lastFollowSend = 0;
  marker.setDraggable(false);
  // Let clicks and moves through to the map: the marker sits under the cursor.
  marker.getElement().style.pointerEvents = 'none';
  map.getCanvas().style.cursor = 'crosshair';
}

export function stopFollow() {
  endFollow(false);
}

export function dispose() {
  window.removeEventListener('keydown', onKeyDown);
  following = null;
  markers.clear();
  map?.remove();
  map = null;
}
