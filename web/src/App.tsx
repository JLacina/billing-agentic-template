import { InvoiceList } from './features/invoices/InvoiceList'
import { fetchInvoices } from './features/invoices/invoicesApi'

function App() {
  return (
    <main>
      <h1>Billing</h1>
      <InvoiceList loadInvoices={fetchInvoices} />
    </main>
  )
}

export default App
