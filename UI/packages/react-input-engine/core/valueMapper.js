// core/valueMapper.js
export const applyValueMapping = (option, map) => {
  const result = {};

  Object.entries(map).forEach(([field, path]) => {
    result[field] = path
      .split(".")
      .reduce((acc, key) => acc?.[key], option);
  });

  return result;
};
