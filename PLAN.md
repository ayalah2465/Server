# Implementation plan / תוכנית עבודה

Approved topic: concerts, instrument categories, limited seats, ticket prices and
simulated payment only. Roles: User and Admin. No card data or real payment processing.

נושא מאושר: קונצרטים, סוגי כלי נגינה, מקומות מוגבלים, מחירי כרטיסים ותשלום מדומה בלבד.
תפקידים: משתמשת ומנהלת. אין איסוף פרטי אשראי או סליקה אמיתית.

| Stage | English tasks and checks | משימות ובדיקות בעברית |
|---|---|---|
| 1 | Approve topic and scope. Check assignment coverage. | אישור נושא והיקף; בדיקת התאמה למטלה. |
| 2 | Create Domain, Application, Infrastructure, API and xUnit/Moq foundation. Restore, build, test dependency boundaries and start API. | הקמת Domain, Application, Infrastructure, API ותשתית בדיקות; שחזור, בנייה, בדיקות תלויות והפעלת השרת. |
| 3 | PostgreSQL, entities, relationships, first migration and seed. Test fresh setup and repeatable initialization. | מסד נתונים, ישויות, קשרים, migration ראשונה ונתוני הדגמה; בדיקת הקמה ואתחול חוזר. |
| 4 | Second migration with concurrency token; transactional booking. Test accepted/rejected bookings and conflicting saves with two DbContexts. | migration שנייה עם token והזמנה בטרנזקציה; בדיקות הצלחה, דחייה והתנגשות בין שני הקשרים. |
| 5 | Registration, JWT and roles. Test login, 401 and 403. | הרשמה, JWT והרשאות; בדיקות התחברות וסטטוסים 401 ו־403. |
| 6 | REST CRUD, DTOs, AutoMapper, validation, filtering, sorting, database pagination, cancellation and Swagger. Test API contracts and simulated payment behavior. | CRUD, מיפוי, ולידציה, סינון, מיון, עימוד במסד, ביטול ו־Swagger; בדיקות API ותשלום מדומה. |
| 7 | JSON errors, CorrelationId and NLog. Test error responses, correlation, conflict warnings and secret exclusion. | שגיאות אחידות, מזהה בקשה ולוגים; בדיקות תגובות, מזהים, אזהרות והיעדר סודות. |
| 8 | Minimal React client; no intermediate approvals. Build and end-to-end checks including 409 and simulated payment. | לקוח React ללא אישורי ביניים; בנייה ובדיקות מקצה לקצה, כולל 409 ותשלום מדומה. |
| 9 | README, demo access, separate GitHub repositories and Render deployments. Test clean setup, all suites and deployed services. | תיעוד, משתמשי הדגמה, מאגרים ופריסות נפרדים; בדיקות הקמה, כלל הבדיקות והשירותים בענן. |

Each server stage ends with test results and waits for student approval.
Shared business logic stays in services, mapping in profiles and error handling
in middleware. Avoid duplicate logic and unnecessary abstractions.

כל שלב שרת מסתיים בתוצאות בדיקה ובהמתנה לאישור הסטודנטית.
הלוגיקה העסקית תרוכז בשירותים, המיפויים ב־Profiles והשגיאות ב־middleware,
תוך הימנעות מכפילות ומהפשטות מיותרות.

Current gate: Stage 4 completed. Transactional booking and optimistic concurrency tests passed. Wait for approval before Stage 5.

מצב נוכחי: שלב 4 הושלם. בדיקות הזמנה טרנזקציונית ותחרות אופטימית עברו. נדרש אישור לפני שלב 5.
