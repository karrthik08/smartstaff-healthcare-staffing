import { Card, CardContent, Typography } from '@mui/material'
import { Bar, BarChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { MetricPoint } from '../api/analytics'

export default function AnalyticsCharts({ charts }: { charts: { title: string; data: MetricPoint[]; kind: 'bar' | 'line' }[] }) {
  return (
    <>
      {charts.map((chart) => (
        <Card key={chart.title}>
          <CardContent>
            <Typography variant="h6" component="h2">{chart.title}</Typography>
            {chart.data.length === 0 ? <Typography>No records in this range.</Typography> : (
              <ResponsiveContainer width="100%" height={240}>
                {chart.kind === 'bar' ? (
                  <BarChart data={chart.data}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="label" />
                    <YAxis allowDecimals={false} />
                    <Tooltip />
                    <Bar dataKey="value" fill="#0f766e" />
                  </BarChart>
                ) : (
                  <LineChart data={chart.data}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="label" />
                    <YAxis allowDecimals={false} />
                    <Tooltip />
                    <Line dataKey="value" stroke="#0f766e" />
                  </LineChart>
                )}
              </ResponsiveContainer>
            )}
          </CardContent>
        </Card>
      ))}
    </>
  )
}
