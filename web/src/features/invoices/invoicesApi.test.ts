import { afterEach, describe, expect, it, vi } from 'vitest'
import { fetchInvoices } from './invoicesApi'

describe('fetchInvoices', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('returns parsed invoices on success', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify([{ id: '1' }]), { status: 200 })))
    await expect(fetchInvoices()).resolves.toEqual([{ id: '1' }])
  })

  it('throws with the status code on failure', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 503 })))
    await expect(fetchInvoices()).rejects.toThrow('HTTP 503')
  })
})
