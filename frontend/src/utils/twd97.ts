import proj4 from 'proj4'

// TWD97 TM2（EPSG:3826）：中央經線 121°E，比例 0.9999，東偏 250000，GRS80
proj4.defs(
  'EPSG:3826',
  '+proj=tmerc +lat_0=0 +lon_0=121 +k=0.9999 +x_0=250000 +y_0=0 +ellps=GRS80 +units=m +no_defs',
)

export function toTwd97(lonDeg: number, latDeg: number): { x: number; y: number } {
  const [x, y] = proj4('WGS84', 'EPSG:3826', [lonDeg, latDeg])
  return { x, y }
}
