# VorTech.App — Décisions actées

## Fiscalité & totaux
- Statut **micro-entrepreneur** → **pas de TVA** affichée ni calculée.
- **Mention 293 B** forcée sur devis/factures tant que `IsMicro = true`.
- Totaux PDF : **Total** uniquement (HT = Net à payer).

## Numérotation & vie des documents
- Numérotation **multi-séries/année** (DEV, FAC, AVOIR…) : `SERIE-YYYY-####`.
- Devis non transformé **> 365 jours** → **Corbeille 30j** puis suppression.

## Réglages & portabilité
- Réglages en **JSON** (`Config/settings.json`) via `ConfigService` (Load/Save).
- Chemins **relatifs** via `Paths.cs` (`Data/`, `Assets/`, `Config/`, `Logs/`).
- Appli **self-contained** (copiable sur clé USB), pas d’install.

## Paiements
- Dictionnaire **Moyens de paiement** (Nom, Frais fixes, %).  
- **Frais non répercutés** : ils n’affectent pas la facture, mais seront traités en dépenses à l’encaissement (plus tard).

## Articles & stock
- Variantes autorisées (SKU/stock par variante).
- **Seuil mini** → alerte.

## UHF
- **Bandes** : 430–470 MHz ; **exclusion** PMR446 (446.000–446.200 MHz).
- **Tolérance** : même fréquence **autorisée** pour **un même client** si **sites auto distants**.
- Export **CSV CHIRP** par modèle ; `.img` conservé en PJ (pas parsé).

## 4G / POC / SIM
- Activation/Suspension via **emails prestataires** (modèles).  
- Alerte J-15 interne (pas de mails clients auto).
- Suivi MRR-like (coûts récurrents vs CA).

## UI / Thème
- **Dark theme** unique (lisible, contrasté).
- Grilles sans murs de traits, hover lisible.
- Dashboard à l’ouverture.

## Sécurité / Simulation
- **Simulation** (mode test) : DB temporaire, pas d’envoi réel.
- Logs Serilog ; plus tard : audit (copies PW, imports, suppressions…).

