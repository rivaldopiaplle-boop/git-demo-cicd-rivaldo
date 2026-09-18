# Le front est statique : nginx le sert et transmet /api au back-end.
FROM nginx:1.27-alpine
# L'image nginx remplit les gabarits de /etc/nginx/templates au démarrage : la
# même image sert en local et chez l'hébergeur, seule ADRESSE_API change.
ENV ADRESSE_API=http://backend:8080
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY index.html style.css app.js taches.js /usr/share/nginx/html/
EXPOSE 80
