import { STATUSES } from '../api.js'

const formatDate = (value) => new Date(value).toLocaleString()

export default function RequestTable({ requests, status, onStatusChange, onRefresh, loading, error }) {
  return (
    <section className="card">
      <div className="toolbar">
        <h2>Solicitudes</h2>
        <label>
          Estado
          <select value={status} onChange={(e) => onStatusChange(e.target.value)}>
            <option value="">Todos</option>
            {STATUSES.map((s) => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
        </label>
        <button type="button" onClick={onRefresh} disabled={loading}>
          {loading ? 'Actualizando…' : 'Refrescar'}
        </button>
      </div>

      {error && <p className="msg error">No se pudo cargar la lista: {error}</p>}

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>External reference</th>
              <th>Customer ID</th>
              <th>Service code</th>
              <th>Descripción</th>
              <th>Estado</th>
              <th>Intentos</th>
              <th>Actualizado</th>
            </tr>
          </thead>
          <tbody>
            {requests.length === 0 ? (
              <tr>
                <td colSpan={7} className="muted center">Sin solicitudes</td>
              </tr>
            ) : (
              requests.map((r) => (
                <tr key={r.id}>
                  <td className="mono">{r.externalReference}</td>
                  <td>{r.customerId}</td>
                  <td>{r.serviceCode}</td>
                  <td>{r.description}</td>
                  <td><span className={`badge ${r.status.toLowerCase()}`}>{r.status}</span></td>
                  <td>{r.attempts}</td>
                  <td>{formatDate(r.updatedAt)}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </section>
  )
}
