import NotificationsIcon from '@mui/icons-material/Notifications'
import { Badge, IconButton, Menu, MenuItem, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { listNotifications, markRead, notificationHref, unreadCount, type AppNotification } from '../api/notifications'
import { apiErrorMessage } from '../api/client'

export default function NotificationBell() {
  const navigate = useNavigate()
  const [count, setCount] = useState(0)
  const [items, setItems] = useState<AppNotification[]>([])
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loaded, setLoaded] = useState(false)

  useEffect(() => {
    let active = true
    unreadCount()
      .then((value) => {
        if (active) setCount(value)
      })
      .catch((caught) => {
        if (active) setError(apiErrorMessage(caught, 'Notifications could not be loaded.'))
      })
    return () => {
      active = false
    }
  }, [])

  async function open(target: HTMLElement) {
    setAnchor(target)
    try {
      const page = await listNotifications()
      setItems(page.items)
      setCount(page.unreadCount)
      setLoaded(true)
      setError(null)
    } catch (caught) {
      setError(apiErrorMessage(caught, 'Notifications could not be loaded.'))
    }
  }

  return (
    <>
      <IconButton color="inherit" aria-label="Notifications" onClick={(event) => void open(event.currentTarget)}>
        <Badge badgeContent={count} color="error">
          <NotificationsIcon />
        </Badge>
      </IconButton>
      <Menu anchorEl={anchor} open={Boolean(anchor)} onClose={() => setAnchor(null)}>
        {error && <MenuItem disabled>{error}</MenuItem>}
        {loaded && items.length === 0 && !error && <MenuItem disabled>No notifications</MenuItem>}
        {items.slice(0, 6).map((item) => (
          <MenuItem key={item.id} onClick={() => {
            void markRead(item.id).then(() => setCount((current) => Math.max(0, current - (item.isRead ? 0 : 1))))
            setAnchor(null)
            navigate(notificationHref(item))
          }}>
            <Typography sx={{ fontWeight: item.isRead ? 400 : 700 }}>{item.title}</Typography>
          </MenuItem>
        ))}
        <MenuItem onClick={() => { setAnchor(null); navigate('/notifications') }}>Notification center</MenuItem>
      </Menu>
    </>
  )
}
