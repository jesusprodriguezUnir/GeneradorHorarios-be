---
name: db-diagram-generator
description: Generates a Mermaid entity-relationship diagram and detailed Spanish markdown documentation of the database schema directly by parsing C# EF Core persistence entities.
---

# DB Diagram Generator

This skill parses the C# domain entity definitions in the project to produce a complete Mermaid ER diagram and a detailed Spanish guide of the database schema.

## Usage

Run the Python parser script to generate or update the database schema documentation:

```powershell
python .agents/skills/db-diagram-generator/scripts/generate_diagram.py
```

This updates the documentation file at [docs/db_schema_diagram.md](../../../docs/db_schema_diagram.md) with:
1. A Mermaid ER diagram showing all tables, attributes, and relationships.
2. A comprehensive table-by-table reference guide in Spanish.

## Structure

- `SKILL.md`: Skill definition and documentation.
- [scripts/generate_diagram.py](scripts/generate_diagram.py): Python script that performs the parsing and markdown generation.
