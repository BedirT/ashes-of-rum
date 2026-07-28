.PHONY: doctor bootstrap verify-fast verify smoke graphical-smoke agent-smoke agent-graphical-smoke pr-ready post-merge static

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

pr-ready:
	@./scripts/harness pr-ready

post-merge:
	@./scripts/harness post-merge

static:
	@./scripts/harness static
