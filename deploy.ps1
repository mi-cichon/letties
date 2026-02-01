$U = "deployer"
$IP = "192.168.0.178"
$D = "/home/deployer/scrabble-game"

tar --exclude="node_modules" --exclude="bin" --exclude="obj" --exclude=".git" -cvzf p.tar.gz src docker-compose.yml Dockerfile

scp p.tar.gz "${U}@${IP}:${D}/"

ssh "${U}@${IP}" "cd ${D} && tar -xvzf p.tar.gz && rm p.tar.gz && docker compose up -d --build --force-recreate"
rm p.tar.gz