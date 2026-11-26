import { createTheme } from '@mui/material/styles'

export const theme = createTheme({
  palette: {
    primary: { main: '#0F3D5E' },
    secondary: { main: '#127C7A' },
    background: { default: '#F3F6F8', paper: '#FFFFFF' },
  },
  typography: {
    fontFamily: '"Segoe UI", "Helvetica Neue", Arial, sans-serif',
    h4: { fontWeight: 650 },
  },
  shape: { borderRadius: 10 },
  components: {
    MuiButtonBase: {
      styleOverrides: {
        root: {
          '&.Mui-focusVisible': {
            outline: '3px solid #127C7A',
            outlineOffset: 2,
          },
        },
      },
    },
    MuiInputBase: {
      styleOverrides: {
        root: {
          '&.Mui-focused': {
            outline: '2px solid #127C7A',
            outlineOffset: 2,
          },
        },
      },
    },
  },
})
