import { Link } from 'react-router-dom'
import { ShoppingList } from '../features/knowledge/ShoppingList'
import { V1Page } from '../components/v1'

/**
 * Was dahaben muss, wer die Abläufe durchführen will — als eigene Seite.
 *
 * Sie gab es schon, aber nur als zugeklappten Block am Fuß der Wissensseite:
 * kein Menüpunkt, kein Suchtreffer, kein eigener Weg. Wer im Laden steht und
 * „Einkaufsliste" ins Suchfeld tippte, bekam „Nichts gefunden" — für die eine
 * Lage, für die diese Liste gemacht ist, war sie damit nicht da.
 */
function ShoppingListPage() {
  return (
    <V1Page
      eyebrow="Wissen"
      title="Einkaufsliste"
      subtitle="Zusammengezogen aus dem Material, das die Abläufe verlangen — jeder Posten einmal, mit dem Ablauf dahinter, der ihn braucht."
      action={<Link className="ls-btn is-small" to="/wissen">Zu den Abläufen</Link>}
    >
      <ShoppingList initialOffen />
    </V1Page>
  )
}

export default ShoppingListPage
