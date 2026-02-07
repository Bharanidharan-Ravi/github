import resolve from "@rollup/plugin-node-resolve";
import commonjs from "@rollup/plugin-commonjs";
import babel from "@rollup/plugin-babel";
import postcss from "rollup-plugin-postcss";

export default {
  input: "src/index.js",
  external: ["react", "react-dom"],
  output: [
    {
      file: "dist/index.cjs.js",
      format: "cjs",
      exports: "named"
    },
    {
      file: "dist/index.esm.js",
      format: "esm"
    }
  ],
  plugins: [
    resolve(),
    commonjs(),
    postcss({
      extract: "styles.css",
      minimize: true
    }),
    babel({
      babelHelpers: "bundled",
      exclude: "node_modules/**"
    })
  ]
};
