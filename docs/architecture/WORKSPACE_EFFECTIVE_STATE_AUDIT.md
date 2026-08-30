# Workspace effective-state audit

Read-only audit of `qa-runtime-data`; no JSON was modified.

## Summary

- Total workspaces: 270
- Current person present: 112
- Current person absent: 158
- Assignment records present: 12
- Assignment records absent: 258
- Automatic: 270
- Manual free: 0
- Manual occupied: 0
- Manual reserved: 0

| State | Before | After |
|---|---:|---:|
| Free | 260 | 158 |
| Occupied | 10 | 112 |
| Reserved | 0 | 0 |

## Per-map effective totals after correction

| Map | Free | Occupied | Reserved | Corrected |
|---|---:|---:|---:|---:|
| norte | 23 | 10 | 0 | 10 |
| nivel3 | 27 | 18 | 0 | 17 |
| sur | 108 | 84 | 0 | 75 |
| id | 0 | 0 | 0 | 0 |
| qc | 0 | 0 | 0 | 0 |

## Corrected inconsistencies

All rows below were previously emitted as `free` because only `assignment.personId` was considered. The current-user fallback `seat.personId` is part of the established workspace presentation contract and now produces the same effective state for backend and frontend consumers.

| mapId | technicalId | displayLocation | reference | currentPerson | configuredState | old effectiveState | new effectiveState | reason |
|---|---|---|---|---|---|---|---|---|
| norte | N-D4 | G-09 | Hervé Dhellot | hdhellot | automatic | free | occupied | current person fallback from seat.personId |
| norte | N-D5 | G-10 | Javier Gómez | jgomez | automatic | free | occupied | current person fallback from seat.personId |
| norte | N-D6 | G-12 | Elena Delgado | edelgado | automatic | free | occupied | current person fallback from seat.personId |
| norte | N-D8 | G-16 | Carmen Kahatt | ckahatt | automatic | free | occupied | current person fallback from seat.personId |
| norte | N-03 | I-07 | Laura Ibarra | libarra | automatic | free | occupied | current person fallback from seat.personId |
| norte | N-09 | I-11 | Silvia Gutierr | sgutierrez | automatic | free | occupied | current person fallback from seat.personId |
| norte | N-10 | J-11 | Laura Sánchez | lsanchez | automatic | free | occupied | current person fallback from seat.personId |
| norte | N-12 | J-12 | Cristina Povedano | cpovedano | automatic | free | occupied | current person fallback from seat.personId |
| norte | N-15 | M-04 | Pablo Zubiaur | pzubiaur | automatic | free | occupied | current person fallback from seat.personId |
| norte | N-16 | N-04 | Antonio Nieto | anieto | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | M-5 | G-03 | Wendy Estupiñán ETT | westupinan | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | N-5 | G-03 | Manoli Tena | mtena | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | Q-5 | L-03 | María Pascual | mpascual | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | S-5 | P-03 | Inmaculada de Juan | idejuan | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | U-5 | R-03 | Sara Romera | sromera | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | N-3 | H-06 | Deborah Sanchez | dsanchez | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | Q-4 | K-05 | Chelo Tudela | ctudela | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | Q-4b | L-06 | Ana Ruiz Madera | aruiz | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | T-4 | P-06 | Silvia Atienza | satienza | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | R-3 | N-07 | Sonia Cruz | scruz | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | S-3 | P-07 | Roberto Rodríguez | rrodriguez | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | T-3 | Q-07 | Sabah Bougrin | sbougrin | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | V-3 | S-07 | Mª Jesús González | mgonzalez | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | V-2 | S-08 | Irene Cristobal Becaria | icristobal | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | Q-1 | L-09 | Isabel Pardos | ipardos | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | S-1 | P-10 | María Martiañez | mmartianez | automatic | free | occupied | current person fallback from seat.personId |
| nivel3 | V-1 | S-10 | Iratxe Pérez | iperez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-T01 | D-04 | Daniel Perez Orive | dperezorive | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-T02 | D-04 | María Sémelas | msemelas | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-T03 | D-05 | Jorge Murillo | jmurillo | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-T05 | E-04 | C. Sanz | csanz | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-T06 | F-04 | S. Cuenca | scuenca | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-T07 | G-04 | L. Ruperez | lruperez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-W01 | D-06 | O. Pelaez | opelaez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-W02 | D-06 | Santiago Naranjo | snaranjo | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-W03 | D-07 | Ana Ramirez | aramirez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-W04 | D-08 | Luis Ruiz | lruiz | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-W06 | D-10 | Marta Martin | mmartin | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-C03 | G-06 | Lucía Bermudez EXT | lbermudez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-C05 | H-06 | M.Sol Herbella | mherbella | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-C08 | E-06 | Juan A Ramos | jaramos | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-C10 | G-06 | Monica Sanchez | msanchez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-C14 | I-07 | Sonia Vela | svela | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-C15 | F-07 | Jesús Valle | jvalle | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-C16 | F-07 | Jose M. Kerstjens | jkerstjens | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-C17 | G-07 | David Fdez. Matarin | dfernandez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-C18 | G-07 | Pablo Bernabé | pbernabe | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-C20 | I-07 | Carlos Carpio | ccarpio | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-D01 | E-08 | Virginia Negro | vnegro | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-D02 | F-08 | Elena M Cuerda | ecuerda | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-D03 | G-08 | Esther Muñoz | emunoz | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-D06 | E-09 | Héctor Navalon | hnavalon | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-D07 | F-09 | Sonia Riahi | sriahi | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-D08 | G-09 | Luis Camacho | lcamacho | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-D12 | F-09 | Javier Grima | jgrima | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-D13 | G-09 | Clara Somoza | csomoza | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-D15 | H-09 | Elena Garcia | egarcia | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-D17 | F-10 | Oscar Pérez | operez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-D24 | G-10 | Ivan Gonzalez | igonzalez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-R02 | I-08 | Valentin Lopez | vlopez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-R05 | I-09 | Rocío Sainz | rsainz | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-R06 | I-09 | Ivan Ventura | iventura | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-R10 | I-10 | Beatriz Cuerda | bcuerda | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-M02 | L-04 | Elisa Borrego | eborrego | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-M03 | M-04 | E. Calleja | ecalleja | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-M04 | K-05 | Ignacio Glez. | igonzalez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-M08 | M-06 | Ana Blasco | ablasco | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-E01 | L-07 | A. Cubos | acubos | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-E02 | M-07 | Paz Martinez | pmartinez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-E12 | L-09 | Lorena Serrano | lserrano | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-E13 | M-10 | Yolanda Mendiol | ymendiola | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-E14 | L-10 | Pilar Alcañiz | palcaniz | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-E15 | M-10 | Alfonso Rguez. | arodriguez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-P03 | O-04 | Lara Vadillo | lvadillo | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-P04 | P-04 | C. Cuevas | ccuevas | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-P07 | S-04 | Sandra Llamera Compliance | sllamera | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-P08 | T-04 | H. Pieper | hpieper | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-P09 | U-05 | Luis Mora | lmora | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-P11 | V-06 | Sonia Masa | smasa | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-O05 | R-06 | Azucena Esteban | aesteban | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-O06 | S-05 | Maria Arizmendi | marizmendi | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-O09 | Q-06 | Cesar Lopez | clopez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-O11 | R-06 | Vladimir Udalov | vudalov | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-O13 | O-07 | Fernando García | fgarcia | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-O16 | Q-07 | Gracia Espinos | gespinosa | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-O17 | S-07 | Beatriz de Rivas | bderivas | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-O21 | P-08 | Chiara Mauriello | cmauriello | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-O24 | P-09 | Begoña Froilan | bfroilan | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-O25 | O-10 | Helena Sullivan | hsullivan | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-O29 | Q-08 | Esther Fdez. | efernandez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-B03 | S-11 | Edu Torne | etorne | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-B06 | M-12 | E. Perez | eperez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-B08 | O-12 | Bea Garcia | bgarcia | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-B10 | P-12 | Joaquin Rico | jrico | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-B11 | Q-12 | Pablo Aviles | paviles | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-L01 | H-12 | Candida Blazquez | cblazquez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-L03 | H-12 | Nelly Rguez. | nrodriguez | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-L04 | I-12 | Leticia Barbero | lbarbero | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-L05 | H-13 | Cristina Espiga | cespiga | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-L07 | G-14 | Maria Julian | mjulian | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-L08 | H-14 | Osvaldo Nestares | onestares | automatic | free | occupied | current person fallback from seat.personId |
| sur | S-L09 | H-14 | Susana SanJose | ssanjose | automatic | free | occupied | current person fallback from seat.personId |

No `occupied + no current person`, `reserved + current assignment`, or cross-consumer state discrepancies remain under the centralized derivation.
