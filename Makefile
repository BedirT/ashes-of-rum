.PHONY: doctor bootstrap verify-fast verify smoke graphical-smoke agent-smoke agent-graphical-smoke house-agent-smoke house-agent-graphical-smoke training-agent-smoke training-agent-graphical-smoke movement-agent-smoke movement-agent-graphical-smoke combat-agent-smoke combat-agent-graphical-smoke economy-agent-smoke economy-agent-graphical-smoke pr-ready post-merge static

doctor:
	@./scripts/harness doctor

bootstrap:
	@./scripts/harness bootstrap

verify-fast:
	@./scripts/harness verify-fast

verify:
	@./scripts/harness verify

smoke:
	@./scripts/harness smoke

graphical-smoke:
	@./scripts/harness graphical-smoke

agent-smoke:
	@./scripts/harness agent-smoke

agent-graphical-smoke:
	@./scripts/harness agent-graphical-smoke

house-agent-smoke:
	@./scripts/harness house-agent-smoke

house-agent-graphical-smoke:
	@./scripts/harness house-agent-graphical-smoke

training-agent-smoke:
	@./scripts/harness training-agent-smoke

training-agent-graphical-smoke:
	@./scripts/harness training-agent-graphical-smoke

movement-agent-smoke:
	@./scripts/harness movement-agent-smoke

movement-agent-graphical-smoke:
	@./scripts/harness movement-agent-graphical-smoke

combat-agent-smoke:
	@./scripts/harness combat-agent-smoke

combat-agent-graphical-smoke:
	@./scripts/harness combat-agent-graphical-smoke

economy-agent-smoke:
	@./scripts/harness economy-agent-smoke

economy-agent-graphical-smoke:
	@./scripts/harness economy-agent-graphical-smoke

pr-ready:
	@./scripts/harness pr-ready

post-merge:
	@./scripts/harness post-merge

static:
	@./scripts/harness static
