import React from 'react';
import PropTypes from 'prop-types';
import BlockInputListItem from './BlockInputListItem';
import {forIn} from "lodash";

export default class BlockInputList extends React.Component {
  static propTypes = {
    items: PropTypes.object.isRequired,
    onCompleteConnector: PropTypes.func.isRequired,
    resources_dict: PropTypes.object.isRequired,
  }

  onMouseUp(i) {
    this.props.onCompleteConnector(i);
  }

  render() {
    let i = 0;
    const inputs = [];
    forIn(this.props.items, (val, key) => {
      inputs.push({type: val.type, name: key, is_array: false});
    })

    return (
      <div className="nodeInputWrapper">
        <ul className="nodeInputList">
          {inputs.map((item) => {
            return (
              <BlockInputListItem
                onMouseUp={(idx) => this.onMouseUp(idx)}
                key={i}
                index={i++}
                item={item}
                resources_dict={this.props.resources_dict}
              />
            );
          })}
        </ul>
      </div>
    );
  }
}
