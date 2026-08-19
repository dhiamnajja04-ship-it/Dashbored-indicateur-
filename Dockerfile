# Image du frontend Angular. Build depuis la racine du dépôt :
#   docker build -t indicateurs/frontend:1.0 .
FROM node:22-alpine AS build
WORKDIR /src

# npm ci exige package-lock.json : build reproductible.
COPY package.json package-lock.json ./
RUN npm ci

COPY . ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime

# nginx sert les fichiers statiques ET relaie /api vers le Gateway :
# le navigateur ne voit qu'une seule origine, donc aucune URL d'API codée en dur
# dans le bundle et aucun souci de CORS.
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Angular 22 (@angular/build:application) produit les fichiers dans dist/<projet>/browser
COPY --from=build /src/dist/dashboard-indicateurs/browser /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
