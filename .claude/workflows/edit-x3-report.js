export const meta = {
  name: 'edit-x3-report',
  description: 'Coordena os agentes x3-crystal-* (dados, layout, gráficos, validação) para editar um relatório Crystal Reports do Sage X3',
  whenToUse: 'Alterações a um .rpt do Sage X3 que envolvam mais do que um tipo de mudança (ex. tabela nova + reposicionar campos + logo) — para uma alteração simples e única, chamar o agente especialista diretamente costuma ser mais rápido.',
  phases: [
    { title: 'Dados' },
    { title: 'Layout' },
    { title: 'Gráficos' },
    { title: 'Validação' },
  ],
}

// args esperado (objeto, não string):
// {
//   rptPath: string,          // .rpt de partida
//   dbTask: string|null,      // descrição da tarefa de dados p/ x3-crystal-db, ou omitir p/ saltar
//   layoutTask: string|null,  // descrição da tarefa de layout p/ x3-crystal-layout, ou omitir p/ saltar
//   graphicsTask: string|null,// descrição da tarefa gráfica p/ x3-crystal-graphics, ou omitir p/ saltar
// }
//
// Cada etapa recebe o caminho do .rpt produzido pela etapa anterior. A validação corre sempre
// no fim, mesmo que só uma etapa tenha corrido.

const STAGE_SCHEMA = {
  type: 'object',
  properties: {
    finalPath: { type: 'string', description: 'Caminho exato (absoluto ou relativo à raiz do repo) do .rpt de teste gravado no fim desta etapa' },
    summary: { type: 'string', description: 'Resumo conciso do que foi feito nesta etapa' },
  },
  required: ['finalPath', 'summary'],
}

const VALIDATE_SCHEMA = {
  type: 'object',
  properties: {
    passed: { type: 'boolean', description: 'true se nenhum problema foi encontrado' },
    findings: { type: 'string', description: 'Detalhe de cada verificação feita, com números concretos (ver formato nas instruções do validador)' },
  },
  required: ['passed', 'findings'],
}

const a = args || {}
if (!a.rptPath) throw new Error('args.rptPath é obrigatório (.rpt de partida)')

let currentPath = a.rptPath
const originalPath = a.rptPath
const summaries = []

phase('Dados')
if (a.dbTask) {
  const r = await agent(
    `Ficheiro .rpt atual (usa este como ponto de partida): ${currentPath}\n\nTarefa: ${a.dbTask}\n\n` +
    `Trabalha sobre uma cópia de teste, valida com Inspect-X3Report.ps1, e no fim indica o caminho exato do .rpt de teste que produziste.`,
    { agentType: 'x3-crystal-db', phase: 'Dados', schema: STAGE_SCHEMA }
  )
  if (r) { currentPath = r.finalPath; summaries.push('[Dados] ' + r.summary); log('x3-crystal-db: ' + r.summary) }
  else log('x3-crystal-db: sem resultado (falhou ou foi ignorado)')
} else {
  log('Dados: sem tarefa, a saltar')
}

phase('Layout')
if (a.layoutTask) {
  const r = await agent(
    `Ficheiro .rpt atual (usa este como ponto de partida, já reflete as alterações de dados anteriores se as houve): ${currentPath}\n\nTarefa: ${a.layoutTask}\n\n` +
    `Trabalha sobre uma cópia de teste, valida com Inspect-X3Report.ps1, e no fim indica o caminho exato do .rpt de teste que produziste.`,
    { agentType: 'x3-crystal-layout', phase: 'Layout', schema: STAGE_SCHEMA }
  )
  if (r) { currentPath = r.finalPath; summaries.push('[Layout] ' + r.summary); log('x3-crystal-layout: ' + r.summary) }
  else log('x3-crystal-layout: sem resultado (falhou ou foi ignorado)')
} else {
  log('Layout: sem tarefa, a saltar')
}

phase('Gráficos')
if (a.graphicsTask) {
  const r = await agent(
    `Ficheiro .rpt atual (usa este como ponto de partida, já reflete as alterações anteriores): ${currentPath}\n\nTarefa: ${a.graphicsTask}\n\n` +
    `Trabalha sobre uma cópia de teste, valida com Inspect-X3Report.ps1, e no fim indica o caminho exato do .rpt de teste que produziste.`,
    { agentType: 'x3-crystal-graphics', phase: 'Gráficos', schema: STAGE_SCHEMA }
  )
  if (r) { currentPath = r.finalPath; summaries.push('[Gráficos] ' + r.summary); log('x3-crystal-graphics: ' + r.summary) }
  else log('x3-crystal-graphics: sem resultado (falhou ou foi ignorado)')
} else {
  log('Gráficos: sem tarefa, a saltar')
}

phase('Validação')
const validation = await agent(
  `Valida este ficheiro: ${currentPath}\n\nFicheiro original antes de todas as alterações desta sessão (para comparação de tabelas/fórmulas/links): ${originalPath}\n\n` +
  `Segue o formato de relatório descrito nas tuas instruções (verificações com números concretos).`,
  { agentType: 'x3-crystal-validator', phase: 'Validação', schema: VALIDATE_SCHEMA }
)

return {
  finalPath: currentPath,
  originalPath: originalPath,
  stageSummaries: summaries,
  validationPassed: validation ? validation.passed : null,
  validationFindings: validation ? validation.findings : 'validação não devolveu resultado',
}
