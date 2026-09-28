const path = require('path');
const base = require('../webpack.config');
module.exports = {
  ...base,
  mode: 'development',
  devtool: 'source-map',
  entry: { mock: path.resolve(__dirname, 'main.tsx') },
  externals: {},
  resolve: {
    ...base.resolve,
    alias: {
      'cs2/api': path.resolve(__dirname, 'mockApi.ts'),
      'cs2/ui': path.resolve(__dirname, 'mockUi.tsx'),
    },
  },
  output: {
    path: path.resolve(__dirname, 'dist'),
    filename: '[name].js',
    publicPath: 'dist/',
  },
  optimization: { minimize: false },
  plugins: base.plugins.slice(0, 1),
};
