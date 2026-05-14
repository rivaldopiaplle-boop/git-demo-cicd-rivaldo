FROM node:20-alpine
WORKDIR /app
COPY package*.json ./
RUN npm install --production || true
COPY . .
CMD ["node", "index.js"]
