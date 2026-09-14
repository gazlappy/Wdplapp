FROM node:22-bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends php-cli php-mysql ca-certificates && rm -rf /var/lib/apt/lists/*
WORKDIR /workspace
CMD ["node", "tools/backend-tests/run.mjs"]
