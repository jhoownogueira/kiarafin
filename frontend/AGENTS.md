<!-- BEGIN:nextjs-agent-rules -->

## This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->

## KiaraFin frontend conventions

- Preserve Next.js App Router, TypeScript strict, Tailwind CSS 4 and the
  SpaProxy integration with the API at `http://localhost:5134`.
- Read the installed Next.js documentation before changing framework APIs.
- Use the official assets in `public/identidade`; do not recolor, distort or
  replace the KiaraFin logo.
- Keep HTTP calls in typed Axios services and server state in TanStack Query.
- Use React Hook Form with Zod for forms and shadcn/Radix primitives for
  interactive UI.
- Never add permanent financial mocks. Use loading, error and empty states when
  the API has no data.
- User-facing copy, dates and currency use pt-BR, `America/Sao_Paulo` and BRL.
- Before handoff, run `npm run lint` and `npm run build`.
