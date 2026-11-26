import { Box, Paper, Stack, Typography } from '@mui/material'
import type { ReactNode } from 'react'

export default function AuthCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Box sx={{ maxWidth: 480, mx: 'auto', mt: { xs: 2, md: 6 } }}>
      <Paper variant="outlined" sx={{ p: { xs: 2.5, md: 4 } }}>
        <Stack spacing={2.5}>
          <Typography variant="h4" component="h1">{title}</Typography>
          {children}
        </Stack>
      </Paper>
    </Box>
  )
}
