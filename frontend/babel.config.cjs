module.exports = {
  presets: [
    ['@babel/preset-env', { targets: { node: 'current' }, modules: 'commonjs' }],
    ['@babel/preset-react', { runtime: 'automatic' }],
    ['@babel/preset-typescript', { allowDeclareFields: true }],
  ],
  env: {
    test: {
      plugins: [
        function replaceImportMeta() {
          return {
            visitor: {
              MemberExpression(path) {
                if (path.node.object.type === 'MetaProperty') {
                  path.replaceWithSourceString('process.env')
                }
              },
            },
          }
        },
      ],
    },
  },
}
