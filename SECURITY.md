# Segurança

O KiaraFin processa informações financeiras e credenciais de serviços externos.
Não publique tokens, payloads bancários, dumps de banco, capturas com dados reais
ou detalhes exploráveis em issues e pull requests.

Para reportar uma vulnerabilidade, use o recurso **Private vulnerability
reporting** do GitHub na aba **Security** do repositório. Inclua o impacto, os
passos mínimos de reprodução e a versão afetada, sempre com dados sintéticos.

Credenciais Pluggy e Telegram, a chave JWT, a senha do PostgreSQL e o segredo do
webhook devem existir somente no ambiente de implantação ou nos GitHub Actions
Secrets. Caso qualquer valor real seja publicado, revogue-o no provedor antes de
remover o conteúdo do repositório.
