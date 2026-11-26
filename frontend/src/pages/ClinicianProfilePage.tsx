import { Alert, Button, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { useEffect, useState } from 'react'
import { addCredential, deleteCredential, getMyProfile, saveMyProfile, type ClinicianProfile } from '../api/recruitment'
import { apiErrorMessage } from '../api/client'

const preferences = ['FullTime', 'PartTime', 'Contract', 'PerDiem', 'Any']
const empty = {
  firstName: '', lastName: '', phoneNumber: '', professionalTitle: '', specialty: '',
  yearsOfExperience: 0, preferredLocations: '', employmentPreference: 'Any', availabilityDate: '',
}

export default function ClinicianProfilePage() {
  const [form, setForm] = useState(empty)
  const [profile, setProfile] = useState<ClinicianProfile | null>(null)
  const [credentialName, setCredentialName] = useState('')
  const [organization, setOrganization] = useState('')
  const [credentialNumber, setCredentialNumber] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => {
    getMyProfile()
      .then((loaded) => {
        setProfile(loaded)
        setForm({
          firstName: loaded.firstName,
          lastName: loaded.lastName,
          phoneNumber: loaded.phoneNumber,
          professionalTitle: loaded.professionalTitle,
          specialty: loaded.specialty,
          yearsOfExperience: loaded.yearsOfExperience,
          preferredLocations: loaded.preferredLocations,
          employmentPreference: loaded.employmentPreference,
          availabilityDate: loaded.availabilityDate ?? '',
        })
      })
      .catch(() => setProfile(null))
  }, [])

  function update(field: keyof typeof empty, value: string) {
    setForm((current) => ({ ...current, [field]: value }))
  }

  async function save() {
    if (!form.firstName.trim() || !form.lastName.trim() || !form.phoneNumber.trim() || !form.professionalTitle.trim() || !form.specialty.trim()) {
      setError('Enter your name, phone, title, and specialty.')
      return
    }
    try {
      const saved = await saveMyProfile({
        ...form,
        yearsOfExperience: Number(form.yearsOfExperience),
        availabilityDate: form.availabilityDate || null,
      })
      setProfile(saved)
      setMessage('Profile saved.')
      setError(null)
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The profile could not be saved.'))
    }
  }

  async function saveCredential() {
    if (!credentialName.trim() || !organization.trim() || !credentialNumber.trim()) {
      setError('Enter the credential name, organization, and number.')
      return
    }
    try {
      await addCredential({
        credentialType: 'License',
        credentialName,
        issuingOrganization: organization,
        credentialNumber,
        issueDate: new Date().toISOString().slice(0, 10),
      })
      setProfile(await getMyProfile())
      setCredentialName('')
      setOrganization('')
      setCredentialNumber('')
      setMessage('Credential saved.')
      setError(null)
    } catch (caught) {
      setError(apiErrorMessage(caught, 'The credential could not be saved.'))
    }
  }

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">Professional profile</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      {message && <Alert severity="success">{message}</Alert>}
      {!profile && <Alert severity="info">Your profile is not complete yet.</Alert>}
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
        <TextField label="First name" value={form.firstName} onChange={(event) => update('firstName', event.target.value)} fullWidth />
        <TextField label="Last name" value={form.lastName} onChange={(event) => update('lastName', event.target.value)} fullWidth />
      </Stack>
      <TextField label="Phone" value={form.phoneNumber} onChange={(event) => update('phoneNumber', event.target.value)} />
      <TextField label="Professional title" value={form.professionalTitle} onChange={(event) => update('professionalTitle', event.target.value)} />
      <TextField label="Specialty" value={form.specialty} onChange={(event) => update('specialty', event.target.value)} />
      <TextField label="Years of experience" type="number" value={form.yearsOfExperience} onChange={(event) => update('yearsOfExperience', event.target.value)} />
      <TextField label="Preferred locations" value={form.preferredLocations} onChange={(event) => update('preferredLocations', event.target.value)} />
      <TextField label="Employment preference" select value={form.employmentPreference} onChange={(event) => update('employmentPreference', event.target.value)}>
        {preferences.map((value) => <MenuItem key={value} value={value}>{value}</MenuItem>)}
      </TextField>
      <TextField label="Availability" type="date" value={form.availabilityDate} onChange={(event) => update('availabilityDate', event.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
      <Button variant="contained" onClick={save} sx={{ alignSelf: 'flex-start' }}>Save profile</Button>
      <Typography variant="h5" component="h2">Credentials</Typography>
      {profile?.credentials.length ? profile.credentials.map((item) => (
        <Stack key={item.id} direction={{ xs: 'column', sm: 'row' }} spacing={1}>
          <Typography sx={{ flexGrow: 1 }}>{item.credentialName} · {item.issuingOrganization} · {item.verificationStatus}</Typography>
          <Button onClick={() => deleteCredential(item.id).then(() => getMyProfile().then(setProfile))}>Remove</Button>
        </Stack>
      )) : <Typography>No credentials yet.</Typography>}
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
        <TextField label="Credential name" value={credentialName} onChange={(event) => setCredentialName(event.target.value)} fullWidth />
        <TextField label="Issuing organization" value={organization} onChange={(event) => setOrganization(event.target.value)} fullWidth />
        <TextField label="Credential number" value={credentialNumber} onChange={(event) => setCredentialNumber(event.target.value)} fullWidth />
        <Button variant="outlined" onClick={saveCredential} sx={{ alignSelf: 'center' }}>Add credential</Button>
      </Stack>
    </Stack>
  )
}
