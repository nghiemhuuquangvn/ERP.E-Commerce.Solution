import { View } from 'react-native'
import Svg, { Circle, Path } from 'react-native-svg'
import { colors } from '@/theme/theme'

const W = 280
const H = 84
const PAD = 6

/** Revenue by day as an area line scaled to its own minimum and maximum; `summary` is what a screen reader says for the whole chart. */
export function RevenueSparkline({ values, summary }: { values: number[]; summary: string }) {
  if (values.length < 2) return null
  const min = Math.min(...values)
  const max = Math.max(...values)
  const span = max - min || 1
  const pts = values.map((v, i) => [(i * W) / (values.length - 1), H - PAD - ((v - min) / span) * (H - 2 * PAD)] as const)
  const line = pts.map(([x, y], i) => `${i ? 'L' : 'M'}${x.toFixed(1)} ${y.toFixed(1)}`).join(' ')
  const last = pts[pts.length - 1]
  return (
    <View accessible accessibilityRole="image" accessibilityLabel={summary}>
      <Svg width="100%" height={H} viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="none">
        <Path d={`${line} L${W} ${H} L0 ${H}Z`} fill={colors.brandAction} fillOpacity={0.14} />
        <Path d={line} fill="none" stroke={colors.brandAction} strokeWidth={2} />
        <Circle cx={last[0] - 3} cy={last[1]} r={3.5} fill={colors.brandAction} />
      </Svg>
    </View>
  )
}
