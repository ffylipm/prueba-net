export const STATUSES = ['Pending', 'Processing', 'Completed', 'Failed']

// Convierte un ProblemDetails / ValidationProblemDetails en un mensaje legible.
async function toError(res) {
  let message = `Error ${res.status}`
  try {
    const problem = await res.json()
    if (problem.errors) {
      message = Object.values(problem.errors).flat().join(' ')
    } else if (problem.detail || problem.title) {
      message = problem.detail ?? problem.title
    }
  } catch {
    /* respuesta sin cuerpo JSON */
  }
  return new Error(message)
}

export async function getRequests(status) {
  const query = status ? `?status=${encodeURIComponent(status)}` : ''
  const res = await fetch(`/api/requests${query}`)
  if (!res.ok) throw await toError(res)
  return res.json()
}

export async function createRequest(data) {
  const res = await fetch('/api/requests', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  })
  if (!res.ok) throw await toError(res)
  return res.json()
}
