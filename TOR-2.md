Наступний крок — сформувати повне ТЗ MVP v0.1. Архітектурна концепція вже визначена; переходити до коду без формалізації контрактів, інваріантів і сценаріїв розгортання зарано.

До MVP мають увійти:

1. Інвентаризація MikroTik-пристроїв і філій.


2. Типи вузлів:

standalone router;

один router із multi-WAN/failover;

VRRP-кластер;

MikroTik switch лише для management-plane firewall.



3. Захищене підключення через RouterOS API-SSL.


4. Імпорт фактичної конфігурації firewall.


5. Канонічна модель:

filter;

address lists;

interface lists;

базові NAT-залежності.



6. Декларативні політики:

company baseline;

site overlay;

node overlay;

контрольовані винятки.



7. Статичний аналіз правил:

дублікати;

shadowing;

unreachable rules;

конфлікти;

ризик втрати management-доступу.



8. Preview і детермінований diff.


9. Безпечне застосування через керовані chains і jump anchors.


10. Rollback для standalone, multi-WAN і VRRP.


11. Drift detection.


12. RBAC і незмінний журнал аудиту.


13. Повний набір integration-тестів на RouterOS CHR.



Поза MVP:

запис switch-chip ACL;

автоматичне виправлення drift;

керування routing, DHCP, DNS та іншими підсистемами RouterOS;

підтримка інших виробників;

мобільний або web-клієнт;

multi-tenant модель.


Наступний документ: MikroTik Firewall Controller — MVP Technical Specification v0.1.

Він має зафіксувати:

1. Scope і термінологію
2. Функціональні вимоги
3. Нефункціональні вимоги
4. Domain model
5. Policy model
6. Topology model
7. RouterOS capability model
8. Deployment state machine
9. VRRP deployment protocol
10. Multi-WAN validation
11. Rollback protocol
12. Drift classification
13. Security model
14. API-контракти
15. Схему БД
16. GUI-модулі і сценарії
17. Помилки та коди станів
18. Acceptance criteria
19. Test strategy
20. Roadmap реалізації

Після цього — bootstrap репозиторію і перший вертикальний зріз:

підключення до RouterOS
→ discovery
→ snapshot
→ canonicalization
→ diff
→ відображення в GUI

Без запису конфігурації на першому етапі. Це дасть перевірену основу для подальшого deployment engine.