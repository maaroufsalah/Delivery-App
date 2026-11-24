namespace Delivery.Application.Parcels.Import;

/// <summary>Tables de `parcel-import.columns.ts`, telles quelles (JSON).</summary>
public static partial class ImportColumns
{
    private const string Json = """
        {
          "fields": [
            "externalCode",
            "recipientName",
            "recipientPhone",
            "city",
            "district",
            "address",
            "cod",
            "description",
            "openOnDelivery",
            "fragile",
            "notes",
            "quantity"
          ],
          "headers": {
            "fr": {
              "externalCode": "Référence",
              "recipientName": "Destinataire",
              "recipientPhone": "Téléphone",
              "city": "Ville",
              "district": "Quartier",
              "address": "Adresse",
              "cod": "COD",
              "description": "Description",
              "openOnDelivery": "Ouverture colis (O/N)",
              "fragile": "Fragile (O/N)",
              "notes": "Notes",
              "quantity": "Quantité"
            },
            "en": {
              "externalCode": "Reference",
              "recipientName": "Recipient",
              "recipientPhone": "Phone",
              "city": "City",
              "district": "District",
              "address": "Address",
              "cod": "COD",
              "description": "Description",
              "openOnDelivery": "Open on delivery (Y/N)",
              "fragile": "Fragile (Y/N)",
              "notes": "Notes",
              "quantity": "Quantity"
            },
            "ar": {
              "externalCode": "المرجع",
              "recipientName": "المرسل إليه",
              "recipientPhone": "الهاتف",
              "city": "المدينة",
              "district": "الحي",
              "address": "العنوان",
              "cod": "المبلغ عند التسليم",
              "description": "الوصف",
              "openOnDelivery": "فتح الطرد (ن/ل)",
              "fragile": "قابل للكسر (ن/ل)",
              "notes": "ملاحظات",
              "quantity": "الكمية"
            }
          },
          "aliases": {
            "externalCode": [
              "reference",
              "ref",
              "referenceclient",
              "codeexterne",
              "commande",
              "order"
            ],
            "recipientName": [
              "destinataire",
              "client",
              "nom",
              "recipient",
              "name"
            ],
            "recipientPhone": [
              "telephone",
              "tel",
              "phone",
              "gsm",
              "mobile"
            ],
            "city": [
              "ville",
              "villedelivraison",
              "villedestination",
              "city"
            ],
            "district": [
              "quartier",
              "district",
              "zone"
            ],
            "address": [
              "adresse",
              "adresselivraison",
              "address"
            ],
            "cod": [
              "cod",
              "montant",
              "prix",
              "montantcod",
              "amount"
            ],
            "description": [
              "description",
              "contenu",
              "produit",
              "designation"
            ],
            "openOnDelivery": [
              "ouverturecolis",
              "ouverture",
              "openondelivery",
              "ouvrir"
            ],
            "fragile": [
              "fragile"
            ],
            "notes": [
              "notes",
              "note",
              "remarque",
              "commentaire",
              "comment"
            ],
            "quantity": [
              "quantite",
              "quantity",
              "qte",
              "qty",
              "nombredecolis",
              "nbcolis",
              "cartons"
            ]
          },
          "required": [
            "recipientName",
            "recipientPhone",
            "city",
            "address",
            "cod",
            "description"
          ],
          "anomalySheet": {
            "fr": {
              "name": "Anomalies",
              "headers": [
                "Ligne",
                "Référence",
                "Colonne",
                "Niveau",
                "Message"
              ],
              "error": "Erreur",
              "warning": "Avertissement"
            },
            "en": {
              "name": "Anomalies",
              "headers": [
                "Row",
                "Reference",
                "Column",
                "Level",
                "Message"
              ],
              "error": "Error",
              "warning": "Warning"
            },
            "ar": {
              "name": "الملاحظات",
              "headers": [
                "السطر",
                "المرجع",
                "العمود",
                "المستوى",
                "الرسالة"
              ],
              "error": "خطأ",
              "warning": "تنبيه"
            }
          },
          "help": {
            "fr": {
              "sheet": "Colis",
              "help": {
                "name": "Aide",
                "title": "Aide à l'import de colis",
                "rows": [
                  [
                    "Comment faire",
                    "Une ligne par colis, à partir de la ligne 3. La ligne 2 est un exemple : effacez-la avant d’importer."
                  ],
                  [
                    "Référence",
                    "Facultative : c’est VOTRE numéro de commande. Elle rend l’import rejouable : une référence déjà importée est refusée, jamais dupliquée."
                  ],
                  [
                    "Téléphone",
                    "Obligatoire. Chiffres et + uniquement, sans espace ni tiret : 0612345678, +212612345678, +33612345678."
                  ],
                  [
                    "Ville",
                    "Obligatoire. À choisir dans la liste déroulante."
                  ],
                  [
                    "Quartier",
                    "Facultatif, en texte libre. La liste propose les quartiers connus, mais toute valeur est acceptée ; une case vide aussi. Un quartier connu de la ville est reconnu sans tenir compte des majuscules ni des accents."
                  ],
                  [
                    "COD",
                    "Montant à encaisser à la livraison, en dirhams. Laissez 0 pour un colis déjà payé."
                  ],
                  [
                    "Frais inclus",
                    "O si le COD comprend déjà les frais de livraison : c’est le cas habituel. N si les frais s’ajoutent au montant."
                  ],
                  [
                    "Ouverture colis",
                    "O si le destinataire peut ouvrir le colis avant de payer."
                  ],
                  [
                    "Quantité",
                    "Facultative : le nombre de cartons d’une même commande (2, 3…). Le frais de livraison est multiplié par ce nombre, et une étiquette s’imprime par carton. Vide ou 1 : un carton."
                  ],
                  [
                    "Limite",
                    "Deux mille lignes par fichier. Au-delà, découpez en plusieurs classeurs."
                  ],
                  [
                    "Après l’envoi",
                    "Rien n’est créé tant que vous n’avez pas confirmé : l’écran affiche d’abord chaque ligne avec ses erreurs, et vous laisse les corriger sur place."
                  ]
                ]
              }
            },
            "en": {
              "sheet": "Parcels",
              "help": {
                "name": "Help",
                "title": "Parcel import help",
                "rows": [
                  [
                    "How to",
                    "One row per parcel, from row 3. Row 2 is an example, delete it before importing."
                  ],
                  [
                    "Reference",
                    "Optional: your own order number. It makes the import replayable: an already imported reference is rejected, never duplicated."
                  ],
                  [
                    "Phone",
                    "Required. Digits and + only, no spaces or dashes: 0612345678, +212612345678, +33612345678."
                  ],
                  [
                    "City",
                    "Required. Pick it from the dropdown."
                  ],
                  [
                    "District",
                    "Optional, free text. The dropdown suggests known districts, but any value is accepted, and so is an empty cell. A known district of the city is recognised regardless of case and accents."
                  ],
                  [
                    "COD",
                    "Amount to collect on delivery, in dirhams. Use 0 for a prepaid parcel."
                  ],
                  [
                    "Fee included",
                    "Y if the COD already covers the delivery fee, the usual case. N if the fee is added on top."
                  ],
                  [
                    "Open on delivery",
                    "Y if the recipient may open the parcel before paying."
                  ],
                  [
                    "Quantity",
                    "Optional: the number of boxes of one order (2, 3…). The delivery fee is multiplied by it, and one label is printed per box. Empty or 1: one box."
                  ],
                  [
                    "Limit",
                    "Two thousand rows per file. Split anything larger."
                  ],
                  [
                    "After upload",
                    "Nothing is created until you confirm: the screen shows every row with its errors first, and lets you fix them in place."
                  ]
                ]
              }
            },
            "ar": {
              "sheet": "الطرود",
              "help": {
                "name": "مساعدة",
                "title": "مساعدة في استيراد الطرود",
                "rows": [
                  [
                    "كيفية الاستعمال",
                    "سطر واحد لكل طرد ابتداء من السطر 3. السطر 2 مثال، احذفه قبل الاستيراد."
                  ],
                  [
                    "المرجع",
                    "اختياري: رقم طلبك. يجعل الاستيراد قابلاً للإعادة: المرجع المستورد سابقاً يُرفض ولا يُكرَّر."
                  ],
                  [
                    "الهاتف",
                    "إلزامي. أرقام وعلامة + فقط، دون مسافات أو شرطات: 0612345678 أو 212612345678+."
                  ],
                  [
                    "المدينة",
                    "إلزامية. اخترها من القائمة."
                  ],
                  [
                    "الحي",
                    "اختياري، نص حر. تقترح القائمة الأحياء المعروفة، لكن تُقبل أي قيمة، وكذلك الخانة الفارغة."
                  ],
                  [
                    "المبلغ عند التسليم",
                    "بالدرهم. اكتب 0 للطرد المدفوع مسبقاً."
                  ],
                  [
                    "الرسوم مضمنة",
                    "ن إذا كان المبلغ يشمل رسوم التوصيل، وهي الحالة المعتادة. ل إذا كانت الرسوم تُضاف."
                  ],
                  [
                    "فتح الطرد",
                    "ن إذا كان يمكن للمرسل إليه فتح الطرد قبل الدفع."
                  ],
                  [
                    "الكمية",
                    "اختيارية، للعملاء المحترفين فقط: عدد الكراتين لطلبية واحدة (2، 3…). تُضرب رسوم التوصيل في هذا العدد، وتُطبع ملصقة لكل كرتونة. فارغة أو 1: كرتونة واحدة. لعميل غير محترف، كمية أكبر من 1 تجعل السطر خاطئاً، كما في الإدخال اليدوي."
                  ],
                  [
                    "الحد الأقصى",
                    "ألفا سطر لكل ملف."
                  ],
                  [
                    "بعد الإرسال",
                    "لا يُنشأ أي شيء قبل التأكيد: تعرض الشاشة كل سطر مع أخطائه وتتيح تصحيحه مباشرة."
                  ]
                ]
              }
            }
          },
          "trueWords": [
            "o",
            "oui",
            "y",
            "yes",
            "true",
            "1",
            "vrai",
            "نعم",
            "ن"
          ],
          "falseWords": [
            "n",
            "non",
            "no",
            "false",
            "0",
            "faux",
            "لا",
            "ل"
          ]
        }
        """;
}
