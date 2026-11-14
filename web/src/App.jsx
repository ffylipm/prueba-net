import { useCallback, useEffect, useState } from 'react'
import { getRequests } from './api.js'
import RequestForm from './components/RequestForm.jsx'
import RequestTable from './components/RequestTable.jsx'

export default function App() {
  const [status, setStatus] = useState('')
  const [requests, setRequests] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setRequests(await getRequests(status))
      setError(null)
    } catch (e) {
      setError(e.message)
    } finally {
      setLoading(false)
    }
  }, [status])

  // Carga al inicio y cada vez que cambia el filtro. Para ver avances del Worker: botón Refrescar.
  useEffect(() => {
    load()
  }, [load])

  return (
    <main className="container">
      <header>
        <h1>Portal de solicitudes</h1>
        <p className="muted">Registra solicitudes y sigue su estado mientras el Worker las procesa.</p>
      </header>

      <RequestForm onCreated={load} />

      <RequestTable
        requests={requests}
        status={status}
        onStatusChange={setStatus}
        onRefresh={load}
        loading={loading}
        error={error}
      />
    </main>
  )
}
