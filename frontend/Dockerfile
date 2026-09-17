# Le front est statique : nginx le sert et transmet /api au back-end.
FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY index.html style.css app.js taches.js /usr/share/nginx/html/
EXPOSE 80
