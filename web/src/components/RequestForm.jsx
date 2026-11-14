import { useState } from 'react'
import { createRequest } from '../api.js'

const EMPTY = { externalReference: '', customerId: '', serviceCode: '', description: '' }

const FIELDS = [
  { name: 'externalReference', label: 'External reference', maxLength: 100 },
  { name: 'customerId', label: 'Customer ID', maxLength: 50 },
  { name: 'serviceCode', label: 'Service code', maxLength: 50 },
]

export default function RequestForm({ onCreated }) {
  const [form, setForm] = useState(EMPTY)
  const [submitting, setSubmitting] = useState(false)
  const [message, setMessage] = useState(null)

  const update = (e) => setForm({ ...form, [e.target.name]: e.target.value })

  const submit = async (e) => {
    e.preventDefault()
    // Evita doble envío por doble clic; el backend igual rechaza duplicados con 409.
    if (submitting) return
    setSubmitting(true)
    setMessage(null)
    try {
      const created = await createRequest(form)
      setMessage({ type: 'ok', text: `Solicitud ${created.externalReference} registrada (${created.status}).` })
      setForm(EMPTY)
      onCreated()
    } catch (err) {
      setMessage({ type: 'error', text: err.message })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className="card">
      <h2>Nueva solicitud</h2>
      <form onSubmit={submit} className="form">
        {FIELDS.map((f) => (
          <label key={f.name}>
            {f.label}
            <input name={f.name} value={form[f.name]} onChange={update} maxLength={f.maxLength} required />
          </label>
        ))}
        <label className="full">
          Descripción
          <textarea name="description" value={form.description} onChange={update} maxLength={500} rows={3} required />
        </label>
        <div className="full actions">
          <button type="submit" disabled={submitting}>
            {submitting ? 'Enviando…' : 'Registrar'}
          </button>
          {message && <span className={`msg ${message.type}`}>{message.text}</span>}
        </div>
      </form>
    </section>
  )
}
