// WGS84 經緯度 → TWD97 TM2（EPSG:3826，中央經線 121°E，比例 0.9999，東偏 250000）
const A = 6378137
const F = 1 / 298.257222101
const E2 = 2 * F - F * F
const EP2 = E2 / (1 - E2)
const K0 = 0.9999
const LON0 = (121 * Math.PI) / 180

export function toTwd97(lonDeg: number, latDeg: number): { x: number; y: number } {
  const phi = (latDeg * Math.PI) / 180
  const lam = (lonDeg * Math.PI) / 180
  const s = Math.sin(phi)
  const c = Math.cos(phi)
  const t = Math.tan(phi) ** 2
  const cc = EP2 * c * c
  const a = (lam - LON0) * c
  const n = A / Math.sqrt(1 - E2 * s * s)
  const e4 = E2 * E2
  const e6 = e4 * E2
  const m =
    A *
    ((1 - E2 / 4 - (3 * e4) / 64 - (5 * e6) / 256) * phi -
      ((3 * E2) / 8 + (3 * e4) / 32 + (45 * e6) / 1024) * Math.sin(2 * phi) +
      ((15 * e4) / 256 + (45 * e6) / 1024) * Math.sin(4 * phi) -
      ((35 * e6) / 3072) * Math.sin(6 * phi))
  const x =
    K0 * n * (a + ((1 - t + cc) * a ** 3) / 6 + ((5 - 18 * t + t * t + 72 * cc - 58 * EP2) * a ** 5) / 120) + 250000
  const y =
    K0 *
    (m +
      n *
        Math.tan(phi) *
        (a ** 2 / 2 + ((5 - t + 9 * cc + 4 * cc * cc) * a ** 4) / 24 + ((61 - 58 * t + t * t + 600 * cc - 330 * EP2) * a ** 6) / 720))
  return { x, y }
}
