import axios from "axios"

type ProblemDetails = { title?: string; detail?: string; errors?: Record<string, string[]> }

export function getApiErrorMessage(error: unknown, fallback = "Não foi possível concluir a operação.") {
  if (!axios.isAxiosError<ProblemDetails>(error)) return fallback
  const problem = error.response?.data
  const validationMessage = problem?.errors && Object.values(problem.errors).flat()[0]
  return validationMessage ?? problem?.detail ?? problem?.title ?? fallback
}
