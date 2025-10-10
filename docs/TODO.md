# VorTech.App — Roadmap / TODO

## Immédiat
- [ ] Réglages: assurer persistance complète (IsMicro, BusinessName, Siret, Iban, Bic, PaymentMethods[]).
- [ ] PDF facture/devis: finaliser mise en page (notes haut/bas + logo + pied), mention 293B auto si IsMicro.
- [ ] Dashboard: réafficher au démarrage + tuiles principales (devis, factures, stock bas, derniers PDF).
- [ ] CRUD “Moyens de paiement” dans SettingsView (DataGrid + Save).
- [ ] Import CSV Clients & Articles (mapping simple + prévisu).

## Prochain (M2)
- [ ] Brancher **SQLite** (Data/app.db) pour Clients/Articles/Devis/Factures.
- [ ] Numérotation **multi-séries/année** (DEV/FAC/AVOIR).
- [ ] Devis > 365j : tache planifiée + corbeille 30j.
- [ ] Écran Stock (seuil mini + alerte).

## M3
- [ ] 4G/POC/SIM : modèles, Association Radio↔SIM, états, activation/suspension (modèles emails).
- [ ] MRR-like : coûts récurrents vs CA.

## M4
- [ ] UHF v1 : dictionnaire fréquences/sites, attributions, export CSV CHIRP.
- [ ] Planche PDF “liste des canaux”.

## Entretien continu
- [ ] Tests unitaires sur services clés (ConfigService, Numérotation, PDF).
- [ ] Sauvegardes (ZIP chiffré) + rotation + export diagnostic (logs/config).
- [ ] Petits raffinements UI (accessibilité, contrastes, focus/hover).

