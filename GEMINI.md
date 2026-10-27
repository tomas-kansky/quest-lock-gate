# Agent Guidelines

Před zahájením práce a pročtením celého projektu načti kontext, globální pravidla a metodiku z centrálního repozitáře [Agentic Design Framework](https://github.com/tomas-kansky/agentic-design-framework) přes Git v konzoli (např. ověř lokální přítomnost v workspace nebo stáhni pomocí `git clone --depth 1 https://github.com/tomas-kansky/agentic-design-framework.git`).

## Pravidla větvení a produkční ochrana
- Hlavní větev `main` je chráněná (**Protected Branch**).
- Veškerý nový vývoj, opravy chyb i refaktoring **musí probíhat ve feature větvích** (`feat/*`, `fix/*`, `docs/*`).
- Před začátkem jakýchkoliv úprav vždy vytvoř novou větev od `main` a ujisti se, že `dotnet test` prochází.
- Podrobný postup pro release a verzování je popsán v [docs/RELEASE_WORKFLOW.md](docs/RELEASE_WORKFLOW.md).
